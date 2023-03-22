using InexperiencedDeveloper.Controllers.Input;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace InexperiencedDeveloper.Gameplay.Selection
{
    public class SelectionManager : MonoBehaviour
    {
        public static event Action s_OnSelectionChanged;

        [Tooltip("Required if using new Input System")]
        [SerializeField] private PlayerInput m_input;

        [Header("Settings")]
        [SerializeField] private LayerMask m_selectableLayer;
        [SerializeField] private GameObject m_selectionCanvasPrefab;
        [SerializeField] private Color m_selectionBoxColor = new Color(0, 1, 0, 0.3f);

        private RectTransform m_selectionBox;
        private Image m_selectionBoxImg;

        private Vector2 m_startPos;
        private Vector2 m_endPos;

        private List<ISelectable> m_selectedUnits = new List<ISelectable>();
        private RaycastHit[] m_hits = new RaycastHit[4];

        private void Awake()
        {
            //Get Player Input if On new InputSystem
            m_input = GetComponent<PlayerInput>();
            m_input.Init();

            //Instantiate Selection Canvas
            if(m_selectionBox == null)
            {
                GameObject canvas = Instantiate(m_selectionCanvasPrefab, Vector3.zero, Quaternion.identity);
                canvas.name = "SelectionCanvas";
                m_selectionBoxImg = canvas.GetComponentInChildren<Image>(true);
                m_selectionBox = m_selectionBoxImg.gameObject.GetComponent<RectTransform>();
                m_selectionBoxImg.color = m_selectionBoxColor;
            }
        }

        private void Update()
        {
            if (m_input.LeftClick)
            {
                m_startPos = m_input.MousePos;
                if(!m_input.ShiftHeld && !m_input.CtrlHeld)
                    ClearSelection();
            }
            if (m_input.LeftClickCx)
            {
                if (m_input.CtrlHeld)
                {
                    DeselectObjects();
                }
                else
                {
                    SelectObjects();
                }
            }
            if (m_input.LeftClickHeld)
            {
                m_endPos = m_input.MousePos;
                DrawSelectionBox();
            }
        }

        /// <summary>
        /// Selects the objects based on the colliders collected from the raycasts/overlap box
        /// </summary>
        private void SelectObjects()
        {
            Collider[] colliders = CaptureSelectedColliders(m_hits);
            foreach (var col in colliders)
            {
                ISelectable selectable = col.GetComponent<ISelectable>();
                if (selectable == null) continue;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(col.transform.position);
                m_selectedUnits.Add(selectable);
                selectable.OnSelect();
            }
            s_OnSelectionChanged?.Invoke();
            m_selectionBox.gameObject.SetActive(false);
        }
        private void DeselectObjects()
        {
            Collider[] colliders = CaptureSelectedColliders(m_hits);
            foreach (var col in colliders)
            {
                ISelectable selectable = col.GetComponent<ISelectable>();
                if (selectable == null) continue;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(col.transform.position);
                if (m_selectedUnits.Contains(selectable))
                {
                    selectable.OnDeselect();
                    m_selectedUnits.Remove(selectable);
                }
            }
            s_OnSelectionChanged?.Invoke();
            m_selectionBox.gameObject.SetActive(false);
        }

        private void DrawSelectionBox()
        {
            if (!m_selectionBox.gameObject.activeSelf)
            {
                m_selectionBox.gameObject.SetActive(true);
            }

            float width = m_endPos.x - m_startPos.x;
            float height = m_endPos.y - m_startPos.y;

            m_selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
            Vector3 selectionCenter = m_startPos + new Vector2(width / 2, height / 2);
            //m_selectionBox.position = m_startPos;
            m_selectionBox.position = selectionCenter;

        }

        /// <summary>
        /// Casts points based on the screen-space position of the selection box
        /// </summary>
        /// <returns></returns>
        private void SelectionRaycast()
        {
            Vector2 point2 = new Vector2(m_startPos.x, m_endPos.y);
            Vector2 point3 = new Vector2(m_endPos.x, m_startPos.y);
            Vector2[] points = new Vector2[4] { m_startPos, point2, point3, m_endPos };
            RaycastHit[] hits = new RaycastHit[4];
            for (int i = 0; i < 4; i++)
            {
                bool hit = Physics.Raycast(Camera.main.ScreenPointToRay(points[i]), out hits[i], Mathf.Infinity);
                m_hits[i] = hit ? hits[i] : m_hits[i];
            }
        }

        /// <summary>
        /// Snapshot of colliders within box of points
        /// </summary>
        /// <param name="hits"></param>
        /// <param name="boxHeight"></param>
        /// <returns></returns>
        private Collider[] CaptureSelectedColliders(RaycastHit[] hits, float boxHeight = 1)
        {
            SelectionRaycast();
            //Calculate the center of the points
            Vector3 center = Vector3.zero;
            foreach(var hit in hits)
            {
                center += hit.point;
            }
            center /= hits.Length;
            //Get the proper orientation
            Vector3[] orientation = GetBoxOrientation(m_startPos, m_endPos, hits);
            //Get the length of the square
            // (TL - BL + TR - BR) / 2
            Vector3 lengthVector = (orientation[1] - orientation[0] + orientation[2] - orientation[3]) / 2;
            //Get the width of the square
            // (TR - TL + BR - BL) / 2
            Vector3 widthVector = (orientation[2] - orientation[1] + orientation[3] - orientation[0]) / 2;
            //Height in this is Z value, for a reason that is beyond me...
            Vector3 size = new Vector3(widthVector.magnitude, lengthVector.magnitude, boxHeight);
            //Calculate the rotation based on the 4 points
            Quaternion rot = CalculateRotation(orientation[0], orientation[1], orientation[2], orientation[3]);
            //Use all of this math to create a good overlap
            return Physics.OverlapBox(center, size / 2, rot, m_selectableLayer);
        }

        private void ClearSelection()
        {
            foreach (ISelectable obj in m_selectedUnits)
            {
                obj.OnDeselect();
            }
            m_selectedUnits.Clear();
            m_hits = new RaycastHit[4];
        }

        /// <summary>
        /// Matrix math that takes points and transforms them into a rotation
        /// </summary>
        /// <param name="bottomLeft"></param>
        /// <param name="topLeft"></param>
        /// <param name="topRight"></param>
        /// <param name="bottomRight"></param>
        /// <returns></returns>
        private Quaternion CalculateRotation(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight)
        {
            Vector3 localX = (bottomRight - bottomLeft).normalized;
            Vector3 localY = (topLeft - bottomLeft).normalized;
            Vector3 localZ = Vector3.Cross(localX, localY);
            Matrix4x4 rotationMatrix = new Matrix4x4(localX, localY, localZ, new Vector4(0, 0, 0, 1));
            return rotationMatrix.rotation;
        }

        /// <summary>
        /// Figure out which points are bottom left, top left, top right, and bottom right in the square (In that order)
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="endPos"></param>
        /// <param name="hits"></param>
        /// <returns></returns>
        private Vector3[] GetBoxOrientation(Vector3 startPos, Vector3 endPos, RaycastHit[] hits)
        {
            Vector3[] orientation = new Vector3[4];
            if (startPos.x < endPos.x && startPos.y < endPos.y)
            {
                //Bottom Left
                orientation[0] = hits[0].point;
                //Top Left
                orientation[1] = hits[1].point;
                //Top Right
                orientation[2] = hits[3].point;
                //Bottom Right
                orientation[3] = hits[2].point;
            }
            else if (startPos.x < endPos.x && startPos.y >= endPos.y)
            {
                orientation[0] = hits[1].point;
                orientation[1] = hits[0].point;
                orientation[2] = hits[2].point;
                orientation[3] = hits[3].point;
            }
            else if (startPos.x >= endPos.x && startPos.y >= endPos.y)
            {
                orientation[0] = hits[3].point;
                orientation[1] = hits[2].point;
                orientation[2] = hits[0].point;
                orientation[3] = hits[1].point;
            }
            else
            {
                orientation[0] = hits[2].point;
                orientation[1] = hits[3].point;
                orientation[2] = hits[1].point;
                orientation[3] = hits[0].point;
            }
            return orientation;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            //Check that it is being run in Play Mode, so it doesn't try to draw this in Editor mode
            if (!EditorApplication.isPlaying) return;
            Gizmos.color = Color.red;
            if (m_input.LeftClickHeld)
            {
                //StartPoint, point2, point3, endPoint;
                Vector2 point2 = new Vector2(m_startPos.x, m_endPos.y);
                Vector2 point3 = new Vector2(m_endPos.x, m_startPos.y);
                Vector2[] points = new Vector2[4] { m_startPos, point2, point3, m_endPos };
                RaycastHit[] hits = new RaycastHit[4];
                Vector3 center = Vector3.zero;
                for (int i = 0; i < 4; i++)
                {
                    Physics.Raycast(Camera.main.ScreenPointToRay(points[i]), out hits[i], Mathf.Infinity);
                    Gizmos.DrawWireSphere(hits[i].point, 0.25f);
                    center += hits[i].point;
                }
                center /= 4;
                Vector3[] orientation = GetBoxOrientation(m_startPos, m_endPos, hits);
                Vector3 heightVector = (orientation[1] - orientation[0] + orientation[2] - orientation[3]) / 2;
                Vector3 widthVector = (orientation[2] - orientation[1] + orientation[3] - orientation[0]) / 2;
                Vector3 size = new Vector3(widthVector.magnitude, heightVector.magnitude, 1);
                // Calculate the up vector as the cross product of localX and localZ
                Quaternion rot = CalculateRotation(orientation[0], orientation[1], orientation[2], orientation[3]);
                Gizmos.matrix = Matrix4x4.TRS(center, rot, size);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }
        }
#endif
    }
}

