# Pille Pille — Supervelocidad y rescates

Prototipo 3D para Windows. Equipo: Josue Padilla, Santiago Roca, Mathew Bruckner, Mateo Roca y Karen Caral. Incluye código fuente, escena Unity, arena, bots y multijugador LAN cliente-servidor. Esta actualización añade baldosas de supervelocidad y rescate previo a la infección.

## Versión del editor

Proyecto compilado con **Unity 2020.3.49f1** para Windows x64. Pasan 90 comprobaciones de mecánicas, 19 de navegación y comportamiento de bots, y 15 de red. Consulta `VALIDACION.md` para los informes y límites de la validación. No necesita paquetes de terceros ni descargar assets. Para jugar el ejecutable no necesitas instalar Unity; para editar o recompilar sí necesitas el editor y una licencia válida.

## Jugar ahora

Abre `PillePille.exe` dentro de `PillePille_Pulido`. Si recibiste el ZIP del juego, extráelo completo antes de abrirlo. Mantén el ejecutable junto a `PillePille_Data`, `MonoBleedingEdge` y `UnityPlayer.dll`. Para probar desde el proyecto, abre `Assets/Scenes/PillePille.unity` con Unity 2020.3.49f1 y pulsa Play. Si recompilas desde el menú del editor, el resultado se guarda por defecto en la carpeta hermana `PillePille_Windows`.

Elige **Clásico**, **Infección** o **Relevo** y pulsa **Jugar con bots**. En práctica de Infección empiezas como superviviente; puedes elegir **Infectado** antes de entrar. La preferencia se mantiene en las revanchas. Los bots calculan rutas alrededor de los obstáculos, utilizan rampas y baldosas, persiguen, huyen y rescatan aliados. Recuperan la ruta si se atascan, sin teletransportarse.

El HUD muestra solo tu papel, tiempo, contadores y dash. Los avisos duran 3 segundos; E y su progreso aparecen únicamente si un superviviente libre está cerca de un atrapado. Un infectado nunca recibe la acción de rescate. ESC o F1 abre controles y reglas completas. Los personajes tienen casco, visor, ropa, extremidades articuladas y animación: turquesa con + para superviviente, ámbar para atrapado y coral con X para infectado.

| Control | Acción |
| --- | --- |
| WASD / flechas | Moverse con respecto a la cámara |
| Espacio | Saltar |
| Shift | Dash en la dirección de movimiento |
| E, mantener | Rescatar un compañero atrapado en Infección |
| Tab, mantener | Clasificación |
| Esc | Menú; pausa únicamente en práctica local |
| M | Activar / silenciar el sonido de contagio |

Los jugadores turquesa escapan; los naranjas persiguen y los amarillos están atrapados. El parpadeo blanco indica inmunidad. Una estela verde y el contador indican supervelocidad. El marcador identifica al jugador local con `[TÚ]`. Todos los equipos LAN deben usar esta misma versión; los clientes anteriores se rechazan con un mensaje.

## Jugar por LAN

1. Conecta los equipos a la misma red local.
2. En el primer equipo, pulsa **Crear sala LAN**. La sala muestra sus direcciones IPv4.
3. En los otros equipos, escribe la IPv4 del anfitrión y pulsa **Unirse por IP**. `127.0.0.1` sirve solo para probar dos instancias en el mismo equipo.
4. Si Windows solicita acceso de red para el juego, permite el acceso en la red privada de confianza. El transporte utiliza **TCP 7777**. Una VPN, el aislamiento de clientes Wi-Fi o un cortafuegos pueden impedir la conexión.
5. El anfitrión selecciona el modo, añade o quita bots si desea y comienza la ronda.

Admite de 2 a 8 participantes, contando bots y anfitrión. Solo se aceptan nuevos jugadores en la sala y entre rondas. Las conexiones tardías reciben un mensaje para volver a intentar al terminar. Si se pierde el anfitrión, los clientes vuelven al menú; no hay migración de anfitrión. La sala no se publica en Internet ni necesita cuentas.

## Reglas implementadas

### Clásico

Un perseguidor pasa la mancha al tocar a otro jugador. Después de 120 segundos gana quien haya acumulado menos tiempo como perseguidor. Se admiten empates. La clasificación muestra segundos pillando.

### Infección

Cada contacto deja al superviviente atrapado e inmóvil durante 10 segundos. Un compañero libre puede acercarse a 2,2 m, sin una pared entre ambos, y mantener E durante 1 segundo continuo. Soltar E, alejarse, quedar atrapado o perder la conexión/entrada interrumpe el rescate y reinicia el progreso. Solo una persona acumula el progreso de cada rescate; un recién llegado no puede quitárselo.

Al completar el rescate, el compañero vuelve a estar libre y ambos reciben 3 segundos de inmunidad. Cada jugador puede ser rescatado varias veces por ronda. Cada nueva captura abre otra ventana completa de 10 segundos; solo se convierte en infectado si ese plazo termina sin ayuda. El infectado inicial no se puede rescatar.

La infección gana en cuanto no quedan supervivientes libres, aunque haya atrapados. Al agotarse los 120 segundos, ganan los supervivientes que siguen libres; un atrapado no cuenta como superviviente ganador. Esta mecánica es exclusiva de Infección.

### Baldosas de supervelocidad — todos los modos

Tres baldosas con flechas verdes dan **+50 % de velocidad de carrera durante 10 segundos** a evasores y perseguidores. Cada baldosa se desactiva durante **20 segundos para todos**. Una segunda baldosa disponible reinicia el efecto a 10 segundos; no acumula velocidad ni duración. El dash conserva su velocidad y recarga originales.

Hay que entrar o volver a pisar la baldosa: quedarse quieto encima no vuelve a activarla al terminar su recarga. Debes estar a la altura de la baldosa; pasar por encima saltando alto o desde una plataforma no la consume. Los atrapados y eliminados tampoco pueden consumirla. Un indicador sobre cada baldosa muestra su disponibilidad.

### Relevo

La mancha lleva una cuenta atrás compartida de 20 segundos: pasarla **no reinicia** la cuenta. Al llegar a cero, se elimina a su portador y empieza otra cuenta con un nuevo portador. Gana el último jugador vivo; si se alcanza el límite global de 120 segundos, se anuncia a todos los supervivientes.

En LAN, Clásico y Relevo, el perseguidor inicial rota entre participantes. La práctica de Infección respeta el papel elegido en el menú. La desconexión de un perseguidor único transfiere el rol a otro jugador. Con menos de dos participantes vivos, se cierra la ronda.

## Métricas y decisiones del prototipo

| Parámetro | Valor |
| --- | --- |
| Cápsula de jugador | Diámetro 1 m, altura 2 m |
| Velocidad evasor | 6 m/s |
| Velocidad perseguidor | 6,9 m/s; +15 % |
| Inmunidad | 3 s |
| Dash | 15 m/s durante 0,18 s |
| Recarga dash | 2,5 s |
| Supervelocidad | +50 % durante 10 s; evasor 9 m/s, perseguidor 10,35 m/s |
| Recarga compartida de baldosa | 20 s |
| Tiempo atrapado | 10 s |
| Rescate | Mantener E 1 s, a 2,2 m, con visión directa |
| Rescates recibidos por jugador | Sin límite por ronda |
| Impulso vertical de salto | 8 m/s |
| Gravedad | 22 m/s² |
| Arena | 36 × 36 m |
| Simulación | 50 actualizaciones por segundo |
| Estado de red | Hasta 20 envíos por segundo |

El GDD no fija duración de ronda, valores de dash, gravedad, cámara ni reglas detalladas de eliminación. Los valores anteriores son decisiones editables del prototipo. Para evitar tanto devoluciones automáticas como cadenas instantáneas, ambos participantes reciben 3 segundos de dispersión tras un contacto y el nuevo perseguidor no puede volver a contagiar durante esa ventana.

La arena incluye una pasarela a 3 m, dos rampas de 25°, una plataforma separada accesible con salto, coberturas, pasos estrechos y rutas alternativas. La cuadrícula del suelo marca intervalos de 2 m. La cámara elevada sigue al jugador; no se necesitan controles de ratón.

## Abrir y editar en Unity

1. En Unity Hub, añade esta carpeta `PillePille` como proyecto existente.
2. Ábrela con **2020.3.49f1**.
3. Abre `Assets/Scenes/PillePille.unity` y pulsa Play.
4. La escena contiene el graybox editable: mueve sus bloques y ajusta sus colisiones directamente. Si eliminas la arena completa, se regenerará al jugar; **Pille Pille → Mostrar graybox en editor** también puede recuperarla.
5. Usa **Pille Pille → Compilar Windows** para generar el ejecutable en la carpeta hermana `PillePille_Windows`. La compilación conserva la escena existente. La opción **Crear o reconstruir escena** sí reemplaza la escena: úsala solo si quieres restaurar el graybox.
6. También puedes ejecutar `Compilar_y_probar.ps1` desde PowerShell para compilar y lanzar las pruebas de integración. Requiere Unity 2020.3.49f1 con licencia activa y el proyecto cerrado en el editor.

### Código

- `Protocol.cs`: mensajes, estados y valores de equilibrio.
- `LanTransport.cs`: transporte TCP con hilos de lectura/escritura y colas; no llama a Unity desde los hilos de red.
- `MatchSimulation.cs`: reglas, puntuación, rondas, contagio y bots reactivos.
- `PlayerMotor.cs`: cápsula, gravedad, saltos, dash e interpolación visual.
- `RunnerAvatar.cs`: modelos de corredores, materiales, identificación de equipos y animación procedural.
- `BotNavigator.cs`: navegación por superficies y obstáculos, búsqueda A*, persecución, huida y rescate.
- `BotVerification.cs`: pruebas de movimiento y comportamiento de bots con física real.
- `ArenaBuilder.cs`: geometría, colisiones, rampas y señalización.
- `SpeedPad.cs`: presentación de las tres baldosas y sus cuentas atrás; las reglas las decide `MatchSimulation`.
- `PilleGame.cs`: entrada, anfitrión/cliente, lobby, HUD, resultados, cámara y audio.
- `ProjectBuilder.cs`: escena y compilación.
- `RuntimeVerification.cs`: pruebas de integración opcionales dentro del ejecutable.
- `PreviewCapture.cs`: captura y auditoría de red opcionales por argumento de lanzamiento.

El servidor decide posiciones, contacto y puntuación. Los clientes envían intenciones de movimiento y muestran estados interpolados. No hay predicción de cliente ni compensación de latencia: el objetivo es una red local de baja latencia. Los bots disponen de navegación por rutas y decisiones de persecución, huida y rescate; no sustituyen el equilibrio y las pruebas con personas.

## Alcance

Entrega de graybox: geometría simple, mecánicas, UI y red. Los modelos finales, mapas temáticos, VFX finales y VR corresponden a las fases posteriores del GDD. Los tres modos previstos se incluyen ya en este prototipo.

## Pruebas reproducibles

Ejecuta `PillePille.exe -batchmode -nographics -pilleTest -logFile pruebas.log`. Al finalizar genera `verificacion.txt` junto al ejecutable y devuelve código 0 si todas las pruebas pasan.

Consulta `VALIDACION.md` para conocer los resultados y los límites de la validación de esta entrega. Los informes originales de la base no prueban estas funciones nuevas.


