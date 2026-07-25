# La Salada — juego de gestión de puestos

Juego 2D top-down (Unity 6 / URP) de gestión y defensa de puestos en un mercado estilo *La Salada*.
Mezcla **tower-defense** (clientes recorren los pasillos y los puestos los "convencen") con
**gestión** (economía, balanzas del negocio, eventos con decisiones y días con cuota).

## Loop de juego
- **Construir** (entre oleadas): comprás y colocás puestos sobre la arena, mirando hacia los pasillos.
- **Oleadas**: los clientes entran por las entradas y caminan por el piso; los puestos disparan y
  acumulan "convencimiento" por facción. Quien más convence hace la venta (si sos vos, ganás plata).
- **Fin del día** (cada 3 oleadas): se cobra una cuota (sueldos + protección), los rivales se expanden
  y salta un **evento** con una decisión que mueve tus balanzas o te da buffs/debuffs.

## Sistemas
- **Grilla/colocación**: arena (puestos) vs piso (caminos) vs entradas/salidas; footprints variables y rotación.
- **Combate por puesto**: cada puesto gestiona su ataque (rango semicircular, cooldown, daño por tipo/facción).
- **Balanzas** (`BusinessMeters`): hostilidad, reputación (daño), felicidad (velocidad), profit (plata/venta).
- **Economía y días**: plata, precios, reembolsos, cuota diaria creciente.
- **Eventos**: personajes, condiciones de disparo, opciones con consecuencias y diálogo final.
- **Celular (UI)**: app de estadísticas (balanzas como barras), construir/demoler, empezar oleada + velocidad.

## Herramientas de editor (menú `Salada`)
- **Map Painter**: pintar mapas (arena/piso/entrada/rivales) y elegir el tile de piso por celda.
- **Event Editor**: crear/listar personajes y eventos; sincronizarlos con el `EventManager`.

## Estructura
- `Assets/Scripts/Placement` — grilla, colocación, mapa.
- `Assets/Scripts/Combat` — oleadas, clientes, combate, proyectiles, economía.
- `Assets/Scripts/Game` — balanzas, eventos, efectos.
- `Assets/Scripts/UI` — celular y popup de eventos.
- `Assets/Scripts/Editor` — Map Painter y Event Editor.
- `Assets/Data` — ScriptableObjects (puestos, mapas, eventos, personajes).

Unity **6000.5.5f1** (URP 2D).
