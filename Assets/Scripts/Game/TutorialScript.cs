using UnityEngine;

namespace Salada.Game
{
    /// <summary>
    /// Textos de cada paso del tutorial (todos configurables desde un solo asset). El TutorialManager
    /// los lee en orden. El personaje que habla (retrato + nombre) sale de 'speaker' (el Encargado).
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialScript", menuName = "Salada/Tutorial Script")]
    public class TutorialScript : ScriptableObject
    {
        public EventCharacter speaker;   // el Encargado
        public string continueLabel = "Dale";

        [Header("Intro / celular")]
        [TextArea] public string intro =
            "¡Buenas! Soy el encargado. Antes de que te tires a la pileta, dejame que te ensene como funcionan las cosas por aca.";
        [TextArea] public string givePhone =
            "Toma, este celular es tuyo. Con esto vas a manejar todo: poner puestos, ver tus numeros y las zonas. No lo pierdas.";

        [Header("Primer puesto + primer cliente")]
        [TextArea] public string placeFirstStall =
            "Arranquemos. Toca el boton de construir un puesto simple y coloca tu primer puesto en el unico lugar libre, al lado de la puerta.";
        [TextArea] public string firstClientComing =
            "¡Perfecto! Mira, ahi viene tu primer cliente. Fijate: tu puesto lo convence solo mientras pasa cerca.";
        [TextArea] public string saleDone =
            "¡Y ahi esta, tu primera venta! Cuando un cliente junta suficiente, te compra a vos. Asi ganas plata.";

        [Header("Estadisticas (una por dialogo)")]
        [TextArea] public string statProfit =
            "Estas son tus GANANCIAS: cuanto le sacas a cada producto. Mas alto = mas plata por venta.";
        [TextArea] public string statHostility =
            "Esta es la HOSTILIDAD: cuanto te odian los competidores. Si sube, se te van a acercar y complicar mas.";
        [TextArea] public string statReputation =
            "Esta es la REPUTACION: cuanto confian los clientes en vos. Mas reputacion = tus puestos convencen mas rapido.";
        [TextArea] public string statHappiness =
            "Y este es el AMBIENTE LABORAL: que tan contentos estan tus empleados. Contentos, tus puestos atienden mas rapido.";
        [TextArea] public string statsWarning =
            "Ojo con estas cuatro: descuidarlas puede traerte problemas serios. Manteneelas cuidadas.";

        [Header("Rival + disputa (etapa 2)")]
        [TextArea] public string rivalAppears =
            "Mira eso... alguien puso un puesto cerca tuyo. Estan queriendo competir con vos. Tene cuidado.";
        [TextArea] public string rivalStoleClient =
            "¿Viste? Te robaron ese cliente. Asi no. Tenemos que hacer algo al respecto.";
        [TextArea] public string mustCompete =
            "Primero, meteles presion: poné un puesto en la MISMA zona que ellos. Dale, colocalo.";
        [TextArea] public string explainDispute =
            "Ahora abri el menu de ZONAS (el boton nuevo del celu), toca la zona en disputa y elegi ATACAR para romperles el puesto. Es un tira y afloja: macha para ganar. Ojo que atacar te baja un poco la reputacion y te sube la hostilidad.";
        [TextArea] public string finish =
            "¡Y listo! Con eso ya sabes todo lo que necesitas para arrancar. El resto lo vas a ir agarrando solo. ¡Mucha suerte!";
    }
}
