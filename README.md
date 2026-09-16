# Simulador balístico · Unity

Proyecto educativo 3D con Rigidbody, FixedJoint y una interfaz hecha con UI Toolkit (UXML + USS). **Unity 6000.4.6f1**, Universal Render Pipeline 17.4.0.

![Escena e interfaz del simulador](Docs/ballistic-lab.png)

## Cómo jugar

1. Abrir esta carpeta desde Unity Hub con la versión indicada.
2. Abrir `Assets/Ballistics/Scenes/BallisticLab.unity` y presionar **Play**.
3. Usar Game View en **16:9**, preferentemente 1280 × 720 o mayor.
4. Ajustar ángulo (5–75°), fuerza de disparo expresada como impulso (5–60 N·s) y masa (0,5 / 1 / 2 kg). Los sliders también tienen entrada numérica.
5. Pulsar **Disparar**. La simulación observa las colisiones y espera a que los cuerpos se detengan, con un máximo de 12 segundos.
6. Leer el reporte y pulsar **Nuevo intento / reconstruir**. Los parámetros se conservan; los objetivos se regeneran y la puntuación vuelve a cero.

Solo hace falta el mouse. Durante el tiro los controles quedan bloqueados. Al concluir, los cuerpos se congelan para conservar el estado que corresponde al reporte; el siguiente intento vuelve a usar cuerpos dinámicos.

## Física y evaluación

- El lanzamiento aplica `Rigidbody.AddForce(dirección * impulso, ForceMode.Impulse)` una sola vez en FixedUpdate. La velocidad inicial es impulso / masa; la gravedad es la del proyecto (−9,81 m/s²). No se mueve el proyectil mediante Transform.
- La "fuerza" de la interfaz es un **impulso en N·s**, no una fuerza continua en N. Es la magnitud correspondiente a ese modo de AddForce: [documentación de Unity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rigidbody.AddForce.html).
- Proyectil esférico con Rigidbody, SphereCollider, detección continua e interpolación. La línea previa es orientativa: una parábola ideal sin colisiones; el disparo real lo resuelve PhysX.
- Nueve bloques de 0,8 kg en tres columnas, con Rigidbody y BoxCollider. Cada columna usa FixedJoints entre bloques y un anclaje cinemático en la base. Resistencia de rotura: 65 N y 45 N·m. Las piezas empiezan dinámicas, incluso antes del disparo.
- Una pieza cuenta como derribada si, al cerrar el intento, su centro está a más de **0,65 m** de la posición inicial o su rotación difiere más de **35°**. Romper un joint por sí solo no suma puntos.
- **Puntuación = 100 × piezas derribadas + 50 si el proyectil colisionó con algún objetivo.** Máximo: 950 puntos. También se consideran impactos después de rodar o rebotar.
- Se esperan al menos tres segundos desde el primer impacto y 0,8 segundos de reposo de todos los cuerpos. Hay límites de tiempo y de salida del campo para que un tiro no bloquee el juego.

## Reporte y datos

La interfaz muestra puntuación, piezas derribadas, duración de observación, tiempo hasta el primer impacto, objeto alcanzado, punto en coordenadas de mundo, magnitud de velocidad relativa e impulso de colisión. El primer impacto puede ser contra el suelo aunque después alcance un objetivo.

Cada intento completado escribe un JSON independiente en `Application.persistentDataPath/Shots`. La ruta efectiva aparece al pie de la pantalla. En Windows, con los ajustes actuales: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/SimuladorBALISTICO/Shots/`.

El JSON contiene fecha UTC, número de intento de la sesión, parámetros de lanzamiento, duración, motivo de finalización, puntuación y **todos los eventos OnCollisionEnter del proyectil**, con tiempo transcurrido desde el lanzamiento y vectores de punto, velocidad relativa e impulso. No se contabilizan como impactos del proyectil las colisiones entre bloques. Cada archivo tiene un identificador único; reiniciar Play no sobreescribe intentos anteriores. Si falla la escritura, el error aparece en pantalla y el reporte se conserva en memoria.

## Organización

| Carpeta | Contenido |
| --- | --- |
| `Assets/Ballistics/Scenes` | Escena lista para jugar, incluida en Build Settings |
| `Assets/Ballistics/Scripts` | Sesión, proyectil, piezas, datos y enlace de interfaz |
| `Assets/Ballistics/Prefabs` | Proyectil y estructura completa con joints conectados |
| `Assets/Ballistics/UI` | UXML, USS y PanelSettings editables |
| `Assets/Ballistics/Materials` | Materiales visuales y de contacto |
| `Assets/Ballistics/Editor` | Generador de escena y prefabs |
| `Tools` | Generación y verificación automatizada mediante Unity Pipeline |

La escena anterior permanece en `Assets/Scenes`. Si había cambios sin guardar, se preservaron en `BeforeBallisticSetup.unity`.

Para regenerar los assets iniciales: menú **Ballistics → Crear o reconstruir escena**. Esto restablece la escena, materiales y prefabs del simulador; guardar antes cualquier personalización. No es necesario ejecutar el generador para jugar un clon del repositorio.

## Verificación

Con el paquete Unity Pipeline del proyecto y su CLI disponibles:

```powershell
unity command --project-path . editor_play
unity command --project-path . --timeout 58 run_script --file Tools/VerifyLab.cs --timeout_ms 55000
unity command --project-path . editor_stop
```

Abrir primero BallisticLab y esperar a que Play termine de cargar. La verificación espera seis segundos sin disparar, comprueba nueve piezas y nueve joints intactos, ejecuta tres tiros y verifica telemetría, puntuación y escritura JSON. Las verificaciones producen archivos de tiro igual que el juego. No ejecutar dos verificaciones simultáneas.

Los controles se pueden verificar manualmente variando cada slider y la masa, comprobando que la velocidad inicial y la trayectoria cambien, y que no sea posible disparar dos veces durante el mismo intento.

La prueba `Tools/VerifyControls.cs`, ejecutada con `run_script` en Play, también comprueba el enlace de sliders, las tres masas, los botones mediante eventos de UI Toolkit, el bloqueo durante el tiro y la actualización del reporte.

Resultados de la verificación en Unity 6000.4.6f1: nueve piezas y nueve joints intactos tras seis segundos; tiros de 7 N·s / 1 kg, 12 N·s / 1 kg y 24 N·s / 2 kg a 30° con 0, 7 y 8 piezas derribadas, respectivamente (50, 750 y 850 puntos). Los resultados exactos pueden variar ligeramente entre plataformas por el solver físico.

## Git y entrega

Versionar `Assets/` (incluidos los `.meta`), `Packages/`, `ProjectSettings/`, `Tools/`, README y `.gitignore`. Se excluyen Library, Temp, Logs, UserSettings, builds y archivos de IDE. El repositorio remoto y la publicación se configuran con la cuenta del autor.

## Video de YouTube

**Enlace: pendiente de grabar y publicar.**

Guion sugerido, 1–3 minutos: mostrar la interfaz y la estabilidad inicial; hacer un tiro a 30° / 7 N·s / 1 kg, otro a 30° / 12 N·s / 1 kg y otro a 30° / 24 N·s / 2 kg; mostrar los reportes y abrir uno de los JSON. Explicar que duplicar impulso y masa conserva la velocidad inicial pero aumenta el momento del proyectil. Sustituir este texto por el enlace real antes de entregar.

