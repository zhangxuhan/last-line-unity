using System;
using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Serializable]
    public class WeaponParameters
    {
        [Min(0)] public int damage = 10;
        [Min(0.01f)] public float shotInterval = 0.35f;
        [Min(0.01f)] public float bulletSpeed = 10f;
        [Min(1)] public int projectileCount = 1;
        [Min(0)] public int penetration = 0;
    }

    [Header("Prefab")]
    [SerializeField] private PlayerBullet m_prefab_player_bullet;
    [Header("Movement")]
    [SerializeField, Min(0f)] private float m_move_speed = 4f;
    [SerializeField, Range(0f, 0.25f)] private float m_viewport_padding = 0.02f;
    [Header("Weapon")]
    [SerializeField] private WeaponParameters m_weapon = new WeaponParameters();
    [SerializeField, Min(0f)] private float m_muzzle_offset = 0.55f;

    private Coroutine m_main_coroutine;
    private Camera m_camera;
    private Vector3 m_aim_direction = Vector3.up;
    private float m_next_shot_time;

    public void StartRunning()
    {
        StopRunning();
        m_camera = Camera.main;
        m_next_shot_time = Time.time;
        m_main_coroutine = StartCoroutine(MainCoroutine());
    }

    public void StopRunning()
    {
        if (m_main_coroutine == null) return;
        StopCoroutine(m_main_coroutine);
        m_main_coroutine = null;
    }

    private IEnumerator MainCoroutine()
    {
        while (true)
        {
            UpdateMovement();
            UpdateAimDirection();
            if (Input.GetMouseButton(0) && Time.time >= m_next_shot_time)
            {
                Fire();
                m_next_shot_time = Time.time + m_weapon.shotInterval;
            }
            yield return null;
        }
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
            || !TryGetPlanePoint(m_camera.ViewportPointToRay(new Vector3(1f - m_viewport_padding, 0.5f)), plane, out Vector3 right))
            return desiredX;

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
    }

    private static bool TryGetPlanePoint(Ray ray, Plane plane, out Vector3 point)
    {
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }
        point = default;
        return false;
    }

    private void Fire()
    {
        if (!m_prefab_player_bullet) return;
        // projectileCount is retained for the later upgrade task; Task 1 fires one trajectory.
        PlayerBullet bullet = Instantiate(m_prefab_player_bullet, transform.parent);
        bullet.transform.position = transform.position + m_aim_direction * m_muzzle_offset;
        bullet.Initialize(m_aim_direction, m_weapon.damage, m_weapon.bulletSpeed, m_weapon.penetration);
    }

    private void OnDisable() => StopRunning();

    private void OnValidate()
    {
        m_move_speed = Mathf.Max(0f, m_move_speed);
        m_muzzle_offset = Mathf.Max(0f, m_muzzle_offset);
        m_weapon.damage = Mathf.Max(0, m_weapon.damage);
        m_weapon.shotInterval = Mathf.Max(0.01f, m_weapon.shotInterval);
        m_weapon.bulletSpeed = Mathf.Max(0.01f, m_weapon.bulletSpeed);
        m_weapon.projectileCount = Mathf.Max(1, m_weapon.projectileCount);
        m_weapon.penetration = Mathf.Max(0, m_weapon.penetration);
    }
}
