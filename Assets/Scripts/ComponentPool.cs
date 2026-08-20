using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPoolable
{
    void OnSpawned();
    void OnDespawned();
}

public sealed class ComponentPool<T> where T : Component, IPoolable
{
    private readonly Func<T> m_factory;
    private readonly Transform m_storage_root;
    private readonly Stack<T> m_inactive = new Stack<T>();
    private readonly HashSet<T> m_active = new HashSet<T>();
    private readonly List<T> m_release_buffer = new List<T>();

    public ComponentPool(Func<T> factory, Transform storageRoot, int initialCapacity)
    {
        m_factory = factory ?? throw new ArgumentNullException(nameof(factory));
        m_storage_root = storageRoot;
        for (int index = 0; index < Mathf.Max(0, initialCapacity); index++)
            m_inactive.Push(CreateInactive());
    }

    public T Get()
    {
        T item = null;
        while (m_inactive.Count > 0 && !item) item = m_inactive.Pop();
        if (!item) item = CreateInactive();
        if (m_storage_root) item.transform.SetParent(m_storage_root, false);
        item.gameObject.SetActive(true);
        item.OnSpawned();
        m_active.Add(item);
        return item;
    }

    public void Release(T item)
    {
        if (!item || !m_active.Remove(item)) return;
        item.OnDespawned();
        if (m_storage_root) item.transform.SetParent(m_storage_root, false);
        item.gameObject.SetActive(false);
        m_inactive.Push(item);
    }

    public void ReleaseAllActive()
    {
        m_release_buffer.Clear();
        foreach (T item in m_active) m_release_buffer.Add(item);
        foreach (T item in m_release_buffer) Release(item);
        m_release_buffer.Clear();
    }

    private T CreateInactive()
    {
        T item = m_factory();
        if (!item) throw new InvalidOperationException($"Pool factory returned no {typeof(T).Name} component.");
        if (m_storage_root) item.transform.SetParent(m_storage_root, false);
        item.OnDespawned();
        item.gameObject.SetActive(false);
        return item;
    }
}

public sealed class PooledSpriteEffect : MonoBehaviour, IPoolable
{
    private SpriteRenderer m_renderer;

    public SpriteRenderer Renderer
    {
        get
        {
            if (!m_renderer) m_renderer = GetComponent<SpriteRenderer>();
            return m_renderer;
        }
    }

    public void OnSpawned()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        if (Renderer) Renderer.enabled = true;
    }

    public void OnDespawned()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        if (Renderer)
        {
            Renderer.color = Color.white;
            Renderer.enabled = false;
        }
    }
}
