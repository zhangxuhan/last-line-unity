using System;
using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Serializable]
    public class WeaponParameters
    {
        [Min(0f)] public float damage = 10f;
        [Min(0.01f)] public float shotInterval = 0.35f;
        [Min(0.01f)] public float bulletSpeed = 10f;
        [Min(1)] public int projectileCount = 1;
        [Min(0)] public int penetration = 0;
        [Min(0f)] public float spreadAngleStep = 8f;
    }

    [Header("Prefab")]
    [SerializeField] private PlayerBullet m_prefab_player_bullet;
    [Header("Movement")]
    [SerializeField, Min(0f)] private float m_move_speed = 4f;
    [SerializeField, Range(0f, 0.25f)] private float m_viewport_padding = 0.02f;
    [Header("Base Weapon")]
    [SerializeField] private WeaponParameters m_weapon = new WeaponParameters();
    [SerializeField, Min(0f)] private float m_muzzle_offset = 0.55f;

    private Coroutine m_main_coroutine;
    private Camera m_camera;
    private WeaponRuntimeState m_runtime_weapon;
    private Vector3 m_aim_direction = Vector3.up;
    private float m_next_shot_time;
    private bool m_block_fire_until_release;
    private Transform m_visual;
    private Transform m_muzzle;

    public WeaponRuntimeState RuntimeWeapon => m_runtime_weapon;

    public void InitializeForStage()
    {
        SetupVisual();
        m_runtime_weapon = new WeaponRuntimeState(m_weapon.damage, m_weapon.shotInterval,
            m_weapon.bulletSpeed, m_weapon.projectileCount, m_weapon.penetration, m_weapon.spreadAngleStep);
        ResumeRunning();
    }

    public void ResumeRunning()
    {
        StopRunning();
        m_camera = Camera.main;
        m_next_shot_time = Time.time;
        m_block_fire_until_release = true;
        m_main_coroutine = StartCoroutine(MainCoroutine());
    }

    public void StopRunning()
    {
        if (m_main_coroutine == null) return;
        StopCoroutine(m_main_coroutine);
        m_main_coroutine = null;
    }

    public bool TryApplyUpgrade(WeaponUpgradeChoice choice)
    {
        return m_runtime_weapon != null && m_runtime_weapon.TryApply(choice);
    }

    private IEnumerator MainCoroutine()
    {
        while (StageLoop.Instance && StageLoop.Instance.IsPlaying)
        {
            UpdateMovement();
            UpdateAimDirection();
            if (!Input.GetMouseButton(0)) m_block_fire_until_release = false;
            if (!m_block_fire_until_release && Input.GetMouseButton(0) && Time.time >= m_next_shot_time)
            {
                Fire();
                m_next_shot_time = Time.time + m_runtime_weapon.FireInterval;
            }
            yield return null;
        }
        m_main_coroutine = null;
    }

    private void UpdateMovement()
    {
        float input = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;
        Vector3 position = transform.position;
        position.x += Mathf.Clamp(input, -1f, 1f) * m_move_speed * Time.deltaTime;
        position.x = ClampToCamera(position.x);
        transform.position = position;
    }

    private float ClampToCamera(float desiredX)
    {
        if (!m_camera) return desiredX;
        Plane plane = new Plane(Vector3.forward, transform.position);
        if (!TryGetPlanePoint(m_camera.ViewportPointToRay(new Vector3(m_viewport_padding, 0.5f)), plane, out Vector3 left)
            || !TryGetPlanePoint(m_camera.ViewportPointToRay(new Vector3(1f - m_viewport_padding, 0.5f)), plane, out Vector3 right)) return desiredX;
        Collider playerCollider = GetComponentInChildren<Collider>();
        float halfWidth = playerCollider ? playerCollider.bounds.extents.x : 0f;
        return Mathf.Clamp(desiredX, Mathf.Min(left.x, right.x) + halfWidth, Mathf.Max(left.x, right.x) - halfWidth);
    }

    private void UpdateAimDirection()
    {
        if (!m_camera) m_camera = Camera.main;
        if (!m_camera) return;
        Plane plane = new Plane(Vector3.forward, transform.position);
        if (!TryGetPlanePoint(m_camera.ScreenPointToRay(Input.mousePosition), plane, out Vector3 mouseWorld)) return;
        Vector3 direction = mouseWorld - transform.position;
        direction.z = 0f;
        if (direction.sqrMagnitude > 0.0001f) m_aim_direction = direction.normalized;
        if (m_visual)
        {
            float angle = Vector3.SignedAngle(Vector3.right, m_aim_direction, Vector3.forward);
            m_visual.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private static bool TryGetPlanePoint(Ray ray, Plane plane, out Vector3 point)
    {
        if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
        point = default;
        return false;
    }

    private void Fire()
    {
        if (!m_prefab_player_bullet || m_runtime_weapon == null || !StageLoop.Instance || !StageLoop.Instance.IsPlaying) return;
        Vector3 muzzlePosition = m_muzzle ? m_muzzle.position : transform.position + m_aim_direction * m_muzzle_offset;
        StageLoop.Instance.Feedback?.PlayShoot(muzzlePosition, m_aim_direction);
        int count = m_runtime_weapon.ProjectileCount;
        for (int index = 0; index < count; index++)
        {
            float angleOffset = (index - (count - 1) / 2f) * m_runtime_weapon.SpreadAngleStep;
            Vector3 direction = Quaternion.AngleAxis(angleOffset, Vector3.forward) * m_aim_direction;
            PlayerBullet bullet = Instantiate(m_prefab_player_bullet, transform.parent);
            bullet.transform.position = muzzlePosition;
            bullet.Initialize(direction, m_runtime_weapon.Damage, m_runtime_weapon.ProjectileSpeed, m_runtime_weapon.PenetrationCount);
        }
    }

    private void SetupVisual()
    {
        if (m_visual) return;
        foreach (MeshRenderer mesh in GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
        GameObject visualObject = new GameObject("AimVisual", typeof(SpriteRenderer));
        m_visual = visualObject.transform;
        m_visual.SetParent(transform, false);
        SpriteRenderer sprite = visualObject.GetComponent<SpriteRenderer>();
        sprite.sprite = Resources.Load<Sprite>("Task5/Art/player");
        sprite.sortingOrder = 2;
        m_visual.localScale = Vector3.one * 1.55f;
        GameObject muzzleObject = new GameObject("Muzzle");
        m_muzzle = muzzleObject.transform;
        m_muzzle.SetParent(m_visual, false);
        m_muzzle.localPosition = new Vector3(m_muzzle_offset / 1.55f, 0f, 0f);
    }

    private void OnDisable() => StopRunning();

    private void OnValidate()
    {
        m_move_speed = Mathf.Max(0f, m_move_speed);
        m_muzzle_offset = Mathf.Max(0f, m_muzzle_offset);
        m_weapon.damage = Mathf.Max(0f, m_weapon.damage);
        m_weapon.shotInterval = Mathf.Max(WeaponRuntimeState.MinimumFireInterval, m_weapon.shotInterval);
        m_weapon.bulletSpeed = Mathf.Max(0.01f, m_weapon.bulletSpeed);
        m_weapon.projectileCount = Mathf.Clamp(m_weapon.projectileCount, 1, WeaponRuntimeState.MaximumProjectileCount);
        m_weapon.penetration = Mathf.Clamp(m_weapon.penetration, 0, WeaponRuntimeState.MaximumPenetrationCount);
        m_weapon.spreadAngleStep = Mathf.Max(0f, m_weapon.spreadAngleStep);
    }
}
