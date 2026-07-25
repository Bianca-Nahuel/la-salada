using System;
using System.Collections.Generic;
using UnityEngine;

namespace Salada.Placement
{
    /// <summary>
    /// Layout autorado de un mapa como grilla de caracteres (se pinta con el Map Painter):
    ///   A = arena (se colocan puestos, no se camina)
    ///   P = piso  (caminan los clientes, no se colocan puestos)
    ///   E = entrada/salida (piso + donde aparecen/salen clientes)
    ///   R = rival rojo (arena + puesto Enemy inicial)
    ///   Y = rival amarillo (arena + puesto Neutral inicial)
    /// (compat: '.' = arena, '#' = piso). rows[0] es la fila SUPERIOR.
    /// </summary>
    [CreateAssetMenu(fileName = "Map", menuName = "Salada/Map Layout")]
    public class MapLayout : ScriptableObject
    {
        [Serializable]
        public struct PrePlacedStall
        {
            public Vector2Int originCell;
            public StallData stall;
            public Owner owner;
        }

        public int width = 16;
        public int height = 13;

        [Tooltip("Grilla de tiles. A=arena P=piso E=entrada R=rival rojo Y=rival amarillo. rows[0]=arriba.")]
        [TextArea(1, 30)]
        public string[] rows;

        [Tooltip("Capa paralela de ZONAS (mismas dimensiones). Un char por celda = id de zona; '.' = sin zona. rows[0]=arriba.")]
        [TextArea(1, 30)]
        public string[] zoneRows;

        [Tooltip("Zona donde arranca el jugador (puede construir ahi al inicio). '.' = ninguna.")]
        public char playerHomeZone = '.';

        [Tooltip("(Opcional/legacy) puestos precolocados con StallData.")]
        public List<PrePlacedStall> prePlaced = new List<PrePlacedStall>();

        public char CharAt(int x, int y)
        {
            int ri = height - 1 - y; // rows[0] = fila de arriba
            if (rows == null || ri < 0 || ri >= rows.Length) return 'A';
            var row = rows[ri];
            return (row != null && x >= 0 && x < row.Length) ? row[x] : 'A';
        }

        // ---- Capa de zonas ----

        public bool HasZoneLayer => zoneRows != null && zoneRows.Length == height;

        /// <summary>Id de zona de una celda; '.' = sin zona.</summary>
        public char ZoneAt(int x, int y)
        {
            int ri = height - 1 - y; // zoneRows[0] = fila de arriba
            if (zoneRows == null || ri < 0 || ri >= zoneRows.Length) return '.';
            var row = zoneRows[ri];
            return (row != null && x >= 0 && x < row.Length) ? row[x] : '.';
        }

        /// <summary>Capa de zonas por celda ('.' = sin zona).</summary>
        public char[,] BuildZones()
        {
            var zones = new char[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    zones[x, y] = ZoneAt(x, y);
            return zones;
        }

        /// <summary>Ids de zona distintos presentes en el mapa (sin '.').</summary>
        public List<char> ZoneIds()
        {
            var set = new List<char>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    char z = ZoneAt(x, y);
                    if (z != '.' && !set.Contains(z)) set.Add(z);
                }
            return set;
        }

        static bool IsFloor(char c) => c == 'P' || c == '#' || c == 'E' || (c >= '0' && c <= '9');

        /// <summary>Indice de tile de piso elegido para esa celda, o -1 si es auto (P/#/E).</summary>
        public int FloorTileIndex(int x, int y)
        {
            char c = CharAt(x, y);
            return (c >= '0' && c <= '9') ? c - '0' : -1;
        }

        /// <summary>CellType por celda: piso (P/#/E) = Aisle; el resto = Grass (arena).</summary>
        public CellType[,] BuildCells()
        {
            var cells = new CellType[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells[x, y] = IsFloor(CharAt(x, y)) ? CellType.Aisle : CellType.Grass;
            return cells;
        }

        /// <summary>Celdas de entrada/salida (donde aparecen y salen los clientes).</summary>
        public List<Vector2Int> EntranceCells()
        {
            var list = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (CharAt(x, y) == 'E') list.Add(new Vector2Int(x, y));
            return list;
        }

        /// <summary>Puestos rivales iniciales pintados en el mapa (1x1 sobre arena).</summary>
        public List<(Vector2Int cell, Owner owner)> RivalStarts()
        {
            var list = new List<(Vector2Int, Owner)>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    char c = CharAt(x, y);
                    if (c == 'R') list.Add((new Vector2Int(x, y), Owner.Enemy));
                    else if (c == 'Y') list.Add((new Vector2Int(x, y), Owner.Neutral));
                }
            return list;
        }
    }
}
