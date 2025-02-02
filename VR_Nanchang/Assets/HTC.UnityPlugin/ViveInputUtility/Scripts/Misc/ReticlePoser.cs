﻿//========= Copyright 2016-2024, HTC Corporation. All rights reserved. ===========

#pragma warning disable 0649
using HTC.UnityPlugin.Pointer3D;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Valve.VR;

public class ReticlePoser : MonoBehaviour
{
    /// <summary>
    /// 射线交互
    /// </summary>
    public interface IMaterialChanger
    {
        Material reticleMaterial { get; }
    }
    
    public Pointer3DRaycaster raycaster;
    [FormerlySerializedAs("Target")]
    [FormerlySerializedAs("reticleForDefaultRay")]
    public Transform reticleForStraightRay;
    public Transform reticleForCurvedRay;
    public bool showOnHitOnly = true;

    public GameObject hitTarget;//击中的物体
    public float hitDistance;
    public Material defaultReticleMaterial;
    public Renderer[] reticleRenderer;

    public bool autoScaleReticle = false;
    public int sizeInPixels = 50;

    [SerializeField]
    private UnityEvent onShowReticleForStraightRay;
    [SerializeField]
    private UnityEvent onShowReticleForCurvedRay;
    [SerializeField]
    private UnityEvent onHideReticle;

    public bool IsReticleVisible { get { return isReticleVisible; } }
    public UnityEvent OnShowReticleForStraightRay { get { return onShowReticleForStraightRay; } }
    public UnityEvent OnShowReticleForCurvedRay { get { return onShowReticleForCurvedRay; } }
    public UnityEvent OnHideReticle { get { return onHideReticle; } }
    [Obsolete("Use reticleForStraightRay instead")]
    public Transform reticleForDefaultRay { get { return reticleForStraightRay; } set { reticleForStraightRay = value; } }

    private bool isReticleVisible;
    private Material m_matFromChanger;


    [Header("需要交互的物体")]
    [SerializeField] private List<GameObject> interactive; // 需要交互的物体
    [SerializeField] private GameObject guideLine_false; // 红色射线
    [SerializeField] private GameObject guideLine_true; // 绿色射线

    // 更新方法，用于控制瞄准标记的显示状态
    private void Update()
    {
        guideLine_false.SetActive(false);
        guideLine_true.SetActive(false);

        // 如果 hitTarget 不为空且在 inter 列表中，则显示 guideLine_true
        if (hitTarget != null && interactive.Contains(hitTarget))
        {
            guideLine_true.SetActive(true);
        }
        else
        {
            guideLine_false.SetActive(true);
        }

        //检测扳机按下开门物体
    }



#if UNITY_EDITOR
    protected virtual void Reset()
    {
        for (var tr = transform; raycaster == null && tr != null; tr = tr.parent)
        {
            raycaster = tr.GetComponentInChildren<Pointer3DRaycaster>(true);
        }

        reticleRenderer = GetComponentsInChildren<Renderer>(true);
    }
#endif
    protected virtual void LateUpdate()
    {
        var points = raycaster.BreakPoints;
        var pointCount = points.Count;
        var result = raycaster.FirstRaycastResult();

        if ((showOnHitOnly && !result.isValid) || pointCount <= 1)
        {
            if (isReticleVisible)
            {
                isReticleVisible = false;
                if (reticleForStraightRay != null) { reticleForStraightRay.gameObject.SetActive(false); }
                if (reticleForCurvedRay != null) { reticleForCurvedRay.gameObject.SetActive(false); }
                if (onHideReticle != null) { onHideReticle.Invoke(); }
            }
            return;
        }

        var isCurvedRay = raycaster.CurrentSegmentGenerator() != null;

        var targetReticle = isCurvedRay ? reticleForCurvedRay : reticleForStraightRay;
        if (result.isValid)
        {
            if (targetReticle != null)
            {
                targetReticle.position = result.worldPosition;
                targetReticle.rotation = Quaternion.LookRotation(result.worldNormal, raycaster.transform.forward);
                if (autoScaleReticle)
                {
                    // Set the reticle size based on sizeInPixels, references:
                    // https://answers.unity.com/questions/268611/with-a-perspective-camera-distance-independent-siz.html
                    Vector3 a = Camera.main.WorldToScreenPoint(targetReticle.position);
                    Vector3 b = new Vector3(a.x, a.y + sizeInPixels, a.z);
                    Vector3 aa = Camera.main.ScreenToWorldPoint(a);
                    Vector3 bb = Camera.main.ScreenToWorldPoint(b);
                    targetReticle.localScale = Vector3.one * (aa - bb).magnitude;
                }
            }

            hitTarget = result.gameObject;
            hitDistance = result.distance;
        }
        else
        {
            if (targetReticle != null)
            {
                targetReticle.position = points[pointCount - 1];
                targetReticle.rotation = Quaternion.LookRotation(points[pointCount - 1] - points[pointCount - 2], raycaster.transform.forward);
            }

            hitTarget = null;
            hitDistance = 0f;
        }

        // Change reticle material according to IReticleMaterialChanger
        var matChanger = hitTarget == null ? null : hitTarget.GetComponentInParent<IMaterialChanger>();
        var newMat = matChanger == null ? null : matChanger.reticleMaterial;
        if (m_matFromChanger != newMat)
        {
            m_matFromChanger = newMat;

            if (newMat != null)
            {
                SetReticleMaterial(newMat);
            }
            else if (defaultReticleMaterial != null)
            {
                SetReticleMaterial(defaultReticleMaterial);
            }
        }

        if (!isReticleVisible)
        {
            isReticleVisible = true;
            if (reticleForStraightRay != null) { reticleForStraightRay.gameObject.SetActive(!isCurvedRay); }
            if (reticleForCurvedRay != null) { reticleForCurvedRay.gameObject.SetActive(isCurvedRay); }
            if (!isCurvedRay) { if (onShowReticleForStraightRay != null) { onShowReticleForStraightRay.Invoke(); } }
            else { if (onShowReticleForCurvedRay != null) { onShowReticleForCurvedRay.Invoke(); } }
        }
    }

    private void SetReticleMaterial(Material mat)
    {
        if (reticleRenderer == null || reticleRenderer.Length == 0) { return; }

        foreach (Renderer mr in reticleRenderer)
        {
            mr.material = mat;
        }
    }

    protected virtual void OnDisable()
    {
        if (isReticleVisible)
        {
            isReticleVisible = false;
            if (reticleForStraightRay != null) { reticleForStraightRay.gameObject.SetActive(false); }
            if (reticleForCurvedRay != null) { reticleForCurvedRay.gameObject.SetActive(false); }
            if (onHideReticle != null) { onHideReticle.Invoke(); }
        }
    }
}
