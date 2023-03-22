using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour, ISelectable
{
    public GameObject GetGameObject() => gameObject;
    public bool IsSelected { get; private set; }

    private Renderer m_renderer;

    private void Awake()
    {
        m_renderer = GetComponent<Renderer>();
        m_renderer.material.color = Color.red;
    }

    public void OnSelect()
    {
        //Perform UI/Particles/Etc here
        m_renderer.material.color = Color.green;
        IsSelected = true;
    }

    public void OnDeselect()
    {
        //Perform UI/Particles/Etc here
        m_renderer.material.color = Color.red;
        IsSelected = false;
    }

}
