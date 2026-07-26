namespace Salada.Audio
{
    /// <summary>
    /// Todos los sonidos del juego. Para agregar uno nuevo: sumar un valor aca y su ruta de
    /// Resources en AudioManager.ResourcePath. Si el archivo de audio todavia no existe en
    /// Assets/Sounds/Resources/, Sfx.Play(...) simplemente no suena (no rompe nada).
    /// </summary>
    public enum SfxId
    {
        PhonePullOut,    // Sacar el celular
        PhonePutAway,    // Guardar el celular
        UIHover,         // Hover boton celular / opcion de evento
        UIClick,         // Click boton celular / opcion de evento
        NewWaveOrEvent,  // Empezar oleada / nuevo dia-evento
        StallPlace,      // Colocar puesto
        StallRotate,     // Rotar puesto
        StallInvalid,    // Colocar en lugar invalido
        StallDemolish,   // Demoler puesto
        StallDestroyed,  // Destruccion de puesto (por evento, etc.)
        StallAttack,     // Ataque de un puesto
        ClientConvinced, // Convencer a un cliente
        MoneyGain,       // Ganar dinero
        GameOver,        // Game over
        GameWin,         // Ganar la partida
        Warning,         // Advertencia (hostilidad/reputacion/felicidad fuera de rango)
    }
}
