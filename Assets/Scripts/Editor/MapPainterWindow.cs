using UnityEngine;
using UnityEditor;
using Salada.Placement;

namespace Salada.EditorTools
{
    /// <summary>
    /// Editor para pintar mapas: grilla clickeable con paleta de tiles
    /// (Arena / Piso / Entrada / Rival Rojo / Rival Amarillo). Edita un MapLayout.
    /// </summary>
    public class MapPainterWindow : EditorWindow
    {
        private enum PaintLayer { Terrain, Zones }

        private MapLayout map;
        private MapLayout _lastMap;
        private int _pendingW, _pendingH;
        private MapTileSet tileSet;
        private char brush = 'P';
        private char zoneBrush = 'a';
        private PaintLayer _layer = PaintLayer.Terrain;
        private bool _showZonesInTerrain;
        private Vector2 scroll;
        private const float CELL = 24f;
        private GUIStyle _tag;

        [MenuItem("Salada/Map Painter")]
        static void Open() => GetWindow<MapPainterWindow>("Map Painter");

        void OnGUI()
        {
            if (_tag == null)
                _tag = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };

            map = (MapLayout)EditorGUILayout.ObjectField("Mapa (MapLayout)", map, typeof(MapLayout), false);
            if (map == null)
            {
                EditorGUILayout.HelpBox("Asigna un MapLayout, o crea uno con: Assets > Create > Salada > Map Layout.", MessageType.Info);
                return;
            }

            // El tamano tipeado se guarda en campos de la ventana (si no, se re-lee de map.* cada
            // repintado y "vuelve" a 16/13). Se reinicia solo al cambiar de mapa.
            if (map != _lastMap) { _lastMap = map; _pendingW = map.width; _pendingH = map.height; }

            EditorGUILayout.BeginHorizontal();
            _pendingW = Mathf.Max(1, EditorGUILayout.IntField("Ancho", _pendingW));
            _pendingH = Mathf.Max(1, EditorGUILayout.IntField("Alto", _pendingH));
            using (new EditorGUI.DisabledScope(_pendingW == map.width && _pendingH == map.height))
                if (GUILayout.Button("Aplicar tamano", GUILayout.Width(120))) Resize(_pendingW, _pendingH);
            EditorGUILayout.EndHorizontal();

            if (tileSet == null)
            {
                var guids = AssetDatabase.FindAssets("t:MapTileSet");
                if (guids.Length > 0) tileSet = AssetDatabase.LoadAssetAtPath<MapTileSet>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            tileSet = (MapTileSet)EditorGUILayout.ObjectField("Tile set (piso)", tileSet, typeof(MapTileSet), false);

            EditorGUILayout.Space(4);
            _layer = (PaintLayer)GUILayout.Toolbar((int)_layer, new[] { "Terreno", "Zonas" });

            if (_layer == PaintLayer.Terrain) DrawTerrainPalette();
            else DrawZonePalette();

            EditorGUILayout.Space(6);
            DrawGrid();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                _layer == PaintLayer.Terrain
                    ? "A=arena   P=piso   E=entrada/salida   R=rival rojo   Y=rival amarillo.   Click o arrastra para pintar."
                    : "Pintá ZONAS (territorio). Cada zona es un id (a,b,c...). '.' = sin zona. Las facciones solo construyen en su zona o adyacentes.",
                MessageType.None);
            if (GUILayout.Button("Guardar")) { EditorUtility.SetDirty(map); AssetDatabase.SaveAssets(); }
        }

        void DrawTerrainPalette()
        {
            EditorGUILayout.LabelField("Pincel", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            BrushButton("Arena", 'A');
            BrushButton("Piso auto", 'P');
            BrushButton("Entrada", 'E');
            BrushButton("Rival Rojo", 'R');
            BrushButton("Rival Amar.", 'Y');
            EditorGUILayout.EndHorizontal();
            _showZonesInTerrain = EditorGUILayout.ToggleLeft("Ver zonas tenues", _showZonesInTerrain);

            if (tileSet != null && tileSet.tiles != null && tileSet.tiles.Length > 0)
            {
                EditorGUILayout.LabelField("Tiles de piso (elegí cual)", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                int max = Mathf.Min(tileSet.tiles.Length, 10);
                for (int i = 0; i < max; i++)
                {
                    char c = (char)('0' + i);
                    var t = tileSet.tiles[i];
                    var tex = t != null ? t.texture : null;
                    var prev = GUI.backgroundColor;
                    if (brush == c) GUI.backgroundColor = Color.cyan;
                    if (GUILayout.Button(tex != null ? new GUIContent(tex) : new GUIContent(i.ToString()), GUILayout.Width(44), GUILayout.Height(44)))
                        brush = c;
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawZonePalette()
        {
            EditorGUILayout.LabelField("Pincel de zona", EditorStyles.boldLabel);

            // ids a mostrar = las zonas ya pintadas + la seleccionada (aunque todavia no se haya pintado)
            var ids = map.ZoneIds();
            if (zoneBrush != '.' && !ids.Contains(zoneBrush)) ids.Add(zoneBrush);
            if (ids.Count == 0) ids.Add('a');
            ids.Sort();

            int perRow = 10;
            for (int i = 0; i < ids.Count; i++)
            {
                if (i % perRow == 0) EditorGUILayout.BeginHorizontal();
                char id = ids[i];
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = ZoneColorFor(id) * (zoneBrush == id ? 1f : 0.6f);
                if (GUILayout.Button(zoneBrush == id ? "[" + id + "]" : id.ToString(), GUILayout.Width(34), GUILayout.Height(24))) zoneBrush = id;
                GUI.backgroundColor = prev;
                if (i % perRow == perRow - 1 || i == ids.Count - 1) EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            var p = GUI.backgroundColor;
            GUI.backgroundColor = zoneBrush == '.' ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            if (GUILayout.Button(zoneBrush == '.' ? "[Borrar zona]" : "Borrar zona", GUILayout.Height(22))) zoneBrush = '.';
            GUI.backgroundColor = p;
            if (GUILayout.Button("+ Nueva zona", GUILayout.Height(22))) zoneBrush = NextFreeZoneId();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Pincel actual: {(zoneBrush == '.' ? "borrar" : zoneBrush.ToString())}", EditorStyles.miniLabel);

            // Zona de inicio del jugador (elegida entre las zonas pintadas)
            var painted = map.ZoneIds();
            var options = new string[painted.Count + 1];
            options[0] = "(ninguna)";
            int cur = 0;
            for (int i = 0; i < painted.Count; i++)
            {
                options[i + 1] = painted[i].ToString();
                if (painted[i] == map.playerHomeZone) cur = i + 1;
            }
            int sel = EditorGUILayout.Popup("Zona inicio jugador", cur, options);
            char newHome = sel == 0 ? '.' : painted[sel - 1];
            if (newHome != map.playerHomeZone)
            {
                Undo.RecordObject(map, "Zona inicio");
                map.playerHomeZone = newHome;
                EditorUtility.SetDirty(map);
            }
        }

        void BrushButton(string label, char c)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = ColorFor(c) * (brush == c ? 1f : 0.55f);
            if (GUILayout.Button(brush == c ? "[" + label + "]" : label, GUILayout.Height(24))) brush = c;
            GUI.backgroundColor = prev;
        }

        void DrawGrid()
        {
            EnsureRows();
            EnsureZoneRows();
            bool zoneLayer = _layer == PaintLayer.Zones;
            float gw = map.width * CELL, gh = map.height * CELL;
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(Mathf.Min(gh + 8, 460)));
            Rect area = GUILayoutUtility.GetRect(gw, gh);

            for (int y = 0; y < map.height; y++)
            {
                int drawY = map.height - 1 - y; // fila 0 (y=height-1) arriba
                for (int x = 0; x < map.width; x++)
                {
                    var r = new Rect(area.x + x * CELL, area.y + drawY * CELL, CELL - 1, CELL - 1);
                    char c = map.CharAt(x, y);
                    int fi = c - '0';
                    if (c >= '0' && c <= '9' && tileSet != null && tileSet.tiles != null && fi < tileSet.tiles.Length && tileSet.tiles[fi] != null && tileSet.tiles[fi].texture != null)
                        GUI.DrawTexture(r, tileSet.tiles[fi].texture, ScaleMode.StretchToFill);
                    else
                        EditorGUI.DrawRect(r, ColorFor(c));

                    char z = map.ZoneAt(x, y);
                    if (zoneLayer)
                    {
                        if (z != '.') { var zc = ZoneColorFor(z); zc.a = 0.55f; EditorGUI.DrawRect(r, zc); GUI.Label(r, z.ToString(), _tag); }
                    }
                    else
                    {
                        if (_showZonesInTerrain && z != '.') { var zc = ZoneColorFor(z); zc.a = 0.25f; EditorGUI.DrawRect(r, zc); }
                        if (c == 'E' || c == 'R' || c == 'Y') GUI.Label(r, c.ToString(), _tag);
                    }
                }
            }

            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && area.Contains(e.mousePosition))
            {
                int cx = Mathf.FloorToInt((e.mousePosition.x - area.x) / CELL);
                int drawY = Mathf.FloorToInt((e.mousePosition.y - area.y) / CELL);
                int cy = map.height - 1 - drawY;
                if (cx >= 0 && cx < map.width && cy >= 0 && cy < map.height)
                {
                    if (zoneLayer) PaintZone(cx, cy, zoneBrush);
                    else Paint(cx, cy, brush);
                    e.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void PaintZone(int x, int y, char c)
        {
            Undo.RecordObject(map, "Pintar zona");
            int ri = map.height - 1 - y;
            var chars = (map.zoneRows[ri] ?? "").PadRight(map.width, '.').ToCharArray();
            chars[x] = c;
            map.zoneRows[ri] = new string(chars);
            EditorUtility.SetDirty(map);
        }

        void EnsureZoneRows()
        {
            if (map.zoneRows == null || map.zoneRows.Length != map.height)
            {
                var nr = new string[map.height];
                for (int i = 0; i < map.height; i++)
                    nr[i] = (map.zoneRows != null && i < map.zoneRows.Length ? (map.zoneRows[i] ?? "") : "").PadRight(map.width, '.').Substring(0, map.width);
                map.zoneRows = nr;
            }
            else
            {
                for (int i = 0; i < map.zoneRows.Length; i++)
                    if (map.zoneRows[i] == null || map.zoneRows[i].Length != map.width)
                        map.zoneRows[i] = (map.zoneRows[i] ?? "").PadRight(map.width, '.').Substring(0, map.width);
            }
        }

        char NextFreeZoneId()
        {
            var used = map.ZoneIds();
            for (char c = 'a'; c <= 'z'; c++)
                if (!used.Contains(c)) return c;
            return 'a';
        }

        static Color ZoneColorFor(char id)
        {
            if (id == '.') return Color.clear;
            float h = ((id * 47) % 360) / 360f;
            return Color.HSVToRGB(h, 0.6f, 0.9f);
        }

        void Paint(int x, int y, char c)
        {
            Undo.RecordObject(map, "Pintar mapa");
            int ri = map.height - 1 - y;
            var chars = (map.rows[ri] ?? "").PadRight(map.width, 'A').ToCharArray();
            chars[x] = c;
            map.rows[ri] = new string(chars);
            EditorUtility.SetDirty(map);
        }

        void EnsureRows()
        {
            if (map.rows == null || map.rows.Length != map.height)
            {
                var nr = new string[map.height];
                for (int i = 0; i < map.height; i++)
                    nr[i] = (map.rows != null && i < map.rows.Length ? (map.rows[i] ?? "") : "").PadRight(map.width, 'A').Substring(0, map.width);
                map.rows = nr;
            }
            else
            {
                for (int i = 0; i < map.rows.Length; i++)
                    if (map.rows[i] == null || map.rows[i].Length != map.width)
                        map.rows[i] = (map.rows[i] ?? "").PadRight(map.width, 'A').Substring(0, map.width);
            }
        }

        void Resize(int w, int h)
        {
            Undo.RecordObject(map, "Redimensionar mapa");
            var old = map.rows;
            int oldH = map.height;
            var nr = new string[h];
            for (int ri = 0; ri < h; ri++)
            {
                var chars = new char[w];
                for (int x = 0; x < w; x++) chars[x] = 'A';
                // conservar contenido existente alineado desde arriba-izquierda
                if (old != null && ri < oldH && ri < old.Length && old[ri] != null)
                    for (int x = 0; x < w && x < old[ri].Length; x++) chars[x] = old[ri][x];
                nr[ri] = new string(chars);
            }
            var oldZ = map.zoneRows;
            var nz = new string[h];
            for (int ri = 0; ri < h; ri++)
            {
                var chars = new char[w];
                for (int x = 0; x < w; x++) chars[x] = '.';
                if (oldZ != null && ri < oldH && ri < oldZ.Length && oldZ[ri] != null)
                    for (int x = 0; x < w && x < oldZ[ri].Length; x++) chars[x] = oldZ[ri][x];
                nz[ri] = new string(chars);
            }

            map.width = w; map.height = h; map.rows = nr; map.zoneRows = nz;
            EditorUtility.SetDirty(map);
        }

        static Color ColorFor(char c)
        {
            switch (c)
            {
                case 'P': return new Color(0.30f, 0.30f, 0.34f); // piso
                case 'E': return new Color(0.20f, 0.80f, 0.95f); // entrada
                case 'R': return new Color(0.90f, 0.25f, 0.20f); // rival rojo
                case 'Y': return new Color(0.95f, 0.85f, 0.20f); // rival amarillo
                default:
                    if (c >= '0' && c <= '9') return new Color(0.30f, 0.30f, 0.34f); // piso (tile)
                    return new Color(0.85f, 0.76f, 0.52f);  // arena
            }
        }
    }
}
