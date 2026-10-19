# Validación del prototipo

Proyecto para Unity 2020.3.49f1, Windows x64. El juego descargable de Releases corresponde a las reglas actuales: rescate de 1 segundo y varios rescates posibles del mismo jugador en una ronda.

## Resultados

- **90/90 comprobaciones de mecánicas correctas.** Informe: [Pruebas/integracion_actual.txt](Pruebas/integracion_actual.txt). Se verificaron movimiento, salto, dash, velocidades, colisiones, baldosas, inmunidad, rescates repetidos, permisos de cada papel, interrupciones, plazos, revancha y los tres modos.
- **20/20 comprobaciones de bots correctas.** Informe: [Pruebas/bots_actual.txt](Pruebas/bots_actual.txt). Los bots rodearon obstáculos, salieron de esquinas, subieron rampas, capturaron objetivos, usaron baldosas y rescataron de nuevo al mismo compañero. En 30 segundos, cuatro bots recorrieron entre 179,80 y 244,47 m; el mayor intervalo inmóvil observado fue de 0,12 s.
- **27/27 comprobaciones de reglas correctas.** Informe: [Pruebas/reglas_actuales.txt](Pruebas/reglas_actuales.txt). Estas pruebas compilan los archivos reales de reglas y simulación con adaptadores para Unity. Se complementan con las pruebas de física real anteriores.
- **15/15 comprobaciones de red correctas.** Informe: [Pruebas/red_actual.txt](Pruebas/red_actual.txt). Se ejecutaron anfitrión y cliente nativos más un cliente de prueba TCP en el mismo equipo: conexión, rechazo de versión anterior, movimiento, salto, dash, entrada vencida, autoridad del servidor y activación remota de una baldosa. Protocolo actual: 3.
- Compilación de Unity para Windows: correcta, sin errores.
- Revisión de la ventana real: menú, selección de papel, inicio como superviviente, HUD reducido y acciones contextuales. El infectado no puede rescatar y no recibe el aviso de E. El escenario de revisión del rescate es una situación controlada, no una partida con varias personas.

## Repetir las pruebas

1. Instala Unity 2020.3.49f1 y activa una licencia válida para editar o compilar.
2. Abre este proyecto desde Unity Hub. La escena principal es `Assets/Scenes/PillePille.unity`.
3. Para generar el ejecutable y ejecutar las pruebas de mecánicas y bots, cierra el proyecto en el editor y ejecuta `Compilar_y_probar.ps1` desde PowerShell. Si Unity está en otra ubicación, ajusta `unityPath` en el script.
4. También puedes compilar con **Pille Pille → Compilar Windows**. Luego ejecuta el juego con `-batchmode -nographics -pilleTest` o `-batchmode -nographics -pilleBotTest`; los informes se guardan junto al ejecutable.
5. Las pruebas de reglas independientes están en `Pruebas/Logica/run_rules_tests.ps1`.
6. El cliente de prueba de transporte es `Pruebas/network_probe_actual.py`. Se conecta al anfitrión iniciado con `-pilleHost -pilleNetAudit`, junto con una instancia cliente `-pilleClient -pilleNetAudit`. Deben conectarse antes de que el anfitrión comience la ronda.

La compilación conserva la escena existente. **Crear o reconstruir escena** sí reemplaza la escena por el graybox inicial.

## Límites

La conexión entre dos computadoras y su Wi-Fi/cortafuegos queda pendiente. El rescate se probó con física real, bots y serialización de sus campos, pero no se hizo una sesión de rescate entre dos personas conectadas. No se hizo una partida extensa con ocho personas.

La navegación de los bots utiliza búsqueda A* sobre la geometría del mapa; las pruebas no garantizan un comportamiento perfecto en cualquier mapa personalizado. Las capturas de personajes son vistas de revisión de los modelos reales.

Los informes de texto, las pruebas reproducibles y las capturas se incluyen en el repositorio. Los registros de máquina, datos locales de licencia y archivos generados por Unity no se publican.
