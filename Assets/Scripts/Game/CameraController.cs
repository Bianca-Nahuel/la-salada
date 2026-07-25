using UnityEngine;
using UnityEngine.InputSystem;
using Salada.Placement;

namespace Salada.Game
{
    /// <summary>
    /// Camara 2D navegable: pan (arrastrar con boton central del mouse, o WASD/flechas), zoom
    /// con la rueda, y arranque centrado en la zona de inicio del jugador. Mantiene la vista
    /// dentro de los limites del mapa.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private float keyPanSpeed = 9f;   // unidades/seg a zoom base
        [SerializeField] private float zoomStep = 0.6f;    // por muesca de rueda
        [SerializeField] private float minZoom = 2.5f;
        [SerializeField] private float startZoom = 5f;

        private Camera _cam;
        private float _maxZoom = 10f;
        private Vector2 _dragOrigin;
        private bool _dragging;

        void Start()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            if (grid == null) grid = FindAnyObjectByType<GridManager>();
            if (grid == null || grid.Model == null) return;

            var m = grid.Model;
            float mapW = m.Width * m.CellSize, mapH = m.Height * m.CellSize;
            // zoom maximo = ver todo el mapa (el que sea mas restrictivo entre alto y ancho)
            _maxZoom = Mathf.Max(mapH * 0.5f, mapW / (2f * Mathf.Max(0.1f, _cam.aspect)));
            _cam.orthographicSize = Mathf.Clamp(startZoom, minZoom, _maxZoom);

            // centrar en la zona de inicio del jugador (si esta definida y existe)
            Vector2 center = new Vector2(m.Origin.x + mapW * 0.5f, m.Origin.y + mapH * 0.5f);
            char home = grid.Layout != null ? grid.Layout.playerHomeZone : '.';
            if (home != '.' && m.CellsOfZone(home).Count > 0) center = m.ZoneCentroidWorld(home);
            MoveTo(center);
        }

        void Update()
        {
            if (grid == null || grid.Model == null) return;
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            // ---- Zoom (rueda) ----
            if (mouse != null)
            {
                float sy = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(sy) > 0.01f)
                    _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - Mathf.Sign(sy) * zoomStep, minZoom, _maxZoom);
            }

            // ---- Pan con teclado ----
            if (kb != null)
            {
                Vector2 dir = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir.x -= 1;
                if (dir != Vector2.zero)
                {
                    float sp = keyPanSpeed * (_cam.orthographicSize / startZoom) * Time.unscaledDeltaTime;
                    MoveBy(dir.normalized * sp);
                }
            }

            // ---- Pan arrastrando con boton central ----
            if (mouse != null)
            {
                if (mouse.middleButton.wasPressedThisFrame)
                {
                    _dragging = true;
                    _dragOrigin = ScreenToWorld(mouse.position.ReadValue());
                }
                else if (mouse.middleButton.isPressed && _dragging)
                {
                    Vector2 diff = _dragOrigin - ScreenToWorld(mouse.position.ReadValue());
                    MoveBy(diff);
                }
                else _dragging = false;
            }

            MoveTo(transform.position); // reclampa (por si cambio el zoom cerca del borde)
        }

        void MoveBy(Vector2 delta) => MoveTo((Vector2)transform.position + delta);

        void MoveTo(Vector2 target)
        {
            var m = grid.Model;
            float minX = m.Origin.x, minY = m.Origin.y;
            float maxX = m.Origin.x + m.Width * m.CellSize;
            float maxY = m.Origin.y + m.Height * m.CellSize;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float x = (maxX - minX) <= 2f * halfW ? (minX + maxX) * 0.5f : Mathf.Clamp(target.x, minX + halfW, maxX - halfW);
            float y = (maxY - minY) <= 2f * halfH ? (minY + maxY) * 0.5f : Mathf.Clamp(target.y, minY + halfH, maxY - halfH);
            transform.position = new Vector3(x, y, transform.position.z);
        }

        Vector2 ScreenToWorld(Vector2 screen)
        {
            var w = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -transform.position.z));
            return new Vector2(w.x, w.y);
        }
    }
}
