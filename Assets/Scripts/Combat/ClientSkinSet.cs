using System.Collections.Generic;
using UnityEngine;
using Salada.Placement;

namespace Salada.Combat
{
    /// <summary>
    /// Sprites de los clientes. Cada "skin" es un cliente (p1..p6) con dos frames de caminata (a/b)
    /// por estado: normal (sin comprar) y una version por faccion cuando compra (azul = jugador,
    /// rojo = Picantes, amarillo = Crowned).
    /// </summary>
    [CreateAssetMenu(fileName = "ClientSkinSet", menuName = "Salada/Client Skin Set")]
    public class ClientSkinSet : ScriptableObject
    {
        [System.Serializable]
        public class Skin
        {
            public string id;
            public Sprite normalA, normalB;
            public Sprite playerA, playerB;   // azul
            public Sprite enemyA, enemyB;     // rojo
            public Sprite neutralA, neutralB; // amarillo

            /// <summary>Frame (a=false / b=true) para el estado dado (o normal si bought=false).</summary>
            public Sprite Frame(bool bought, Owner faction, bool b)
            {
                if (!bought) return b ? normalB : normalA;
                switch (faction)
                {
                    case Owner.Player: return b ? playerB : playerA;
                    case Owner.Enemy: return b ? enemyB : enemyA;
                    default: return b ? neutralB : neutralA;
                }
            }
        }

        public List<Skin> skins = new List<Skin>();
    }
}
