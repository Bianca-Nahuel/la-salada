namespace Salada.Placement
{
    /// <summary>Dueño de un puesto. Determina color y si es removible.</summary>
    public enum Owner
    {
        Player,
        Neutral,
        Enemy
    }

    public static class OwnerNames
    {
        /// <summary>
        /// Nombre de la faccion para mostrarle al jugador. No renombramos el enum (rompe datos
        /// serializados y medio codigo): rojo (Enemy) = "Picantes", amarillo (Neutral) = "Crowned".
        /// </summary>
        public static string Display(this Owner o)
        {
            switch (o)
            {
                case Owner.Player: return "vos";
                case Owner.Neutral: return "Crowned";  // amarillo
                case Owner.Enemy: return "Picantes";   // rojo
                default: return o.ToString();
            }
        }
    }
}
