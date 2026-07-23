# Plan de limpieza arquitectónica, auditoría UX y scaffolding

Estado: plan maestro. No autoriza por sí solo cambios de código, datos o UX.

Punto de partida: `main` en `e976d8d0`, después de retirar Simplified Editor y
unificar la edición de los elementos de Status Bar y Navigation Bar.

Este documento ordena los dos siguientes objetivos del proyecto:

1. completar la limpieza arquitectónica y la revisión integral de UX;
2. establecer un procedimiento estricto y repetible para que Codex cree nuevos
   Atoms, Components y Modules a partir de una descripción del usuario.

El plan describe resultados, decisiones y criterios de aceptación. No prescribe
archivos, clases ni una implementación concreta.

## 1. Principios de ejecución

- La limpieza arquitectónica precede a la creación de nuevas definiciones.
- Primero se observa y se localiza la propiedad de un problema; después se
  propone una solución y se pide aprobación.
- Cada fase debe dejar la aplicación utilizable, validada y documentada.
- Las mejoras visuales rápidas se separan de los cambios de propiedad o modelo.
- No se crea una segunda versión reducida del editor. La simplificación futura
  será organización y revelado progresivo dentro del editor completo.
- Diseño y Producción son procesos distintos. La aplicación debe conectarlos
  sin mezclarlos ni ocultar qué tipo de dato se está editando.
- El usuario debe comprender la consecuencia de una acción sin necesitar ver
  vocabulario interno de almacenamiento.
- Los flujos frecuentes deben ser directos; los contratos avanzados deben
  continuar accesibles para trabajo experto.

## 2. Invariantes que no se negocian

Las fases de este plan deben conservar:

- identificadores estables y referencias completas a Variants;
- selección explícita de la Variant por defecto al cruzar una nueva frontera;
- Forwarding y Overrides locales explícitos;
- keyframes relativos a su propietario temporal y ligados por id estable;
- resolución completa del frame antes de Preview;
- renderer genérico y sin conocimiento de Components o Modules concretos;
- `MainWindow` limitado al shell y la coordinación general;
- campos editables a través del diccionario y sus controles compartidos;
- inicio normal de la aplicación estrictamente de solo lectura;
- migraciones explícitas, temporales y sin fallbacks de compatibilidad;
- Apps y Modules creados solo mediante un proceso de desarrollo completo;
- separación entre Test Values de Diseño y payload persistente de Producción;
- estado visual del editor limitado a la sesión.

No se propondrán automatismos basados en nombres visibles, tipos aparentes,
orden, posición, profundidad del árbol o índices de una colección.

## 3. Secuencia global

```text
Base actual validada
  -> inventario y registro de deuda
  -> limpieza de propiedad y persistencia
  -> auditoría UX de Diseño
  -> auditoría UX de Producción
  -> auditoría de navegación y contexto compartido
  -> propuesta UX priorizada y aprobada
  -> olas de mejora controladas
  -> baseline arquitectónico limpio
  -> procedimiento de scaffolding operativo
```

El procedimiento de scaffolding se define en este documento para evitar nuevas
desviaciones, pero no se automatizará ni se usará para ampliar el catálogo hasta
que el baseline limpio haya sido aceptado.

## 4. Línea A — limpieza arquitectónica y revisión integral de UX

### A0. Baseline y registro único de auditoría

Objetivo: asegurar que todas las fases parten del mismo estado y que ningún
hallazgo se pierde o se resuelve en la capa equivocada.

Actividades:

- confirmar rama, commit, sincronización y árbol limpio;
- abrir la aplicación y registrar la versión observada;
- crear un registro único de hallazgos, decisiones y evidencias;
- asignar cada hallazgo a una de estas categorías:
  - complejidad necesaria del modelo;
  - complejidad accidental de la interfaz;
  - terminología;
  - duplicación de interacción;
  - navegación o pérdida de contexto;
  - funcionalidad incompleta;
  - posible violación de fronteras;
  - simplificación segura;
- distinguir claramente defecto actual, mejora posible y trabajo futuro.

Entregables:

- mapa resumido del sistema vigente;
- registro priorizado con pasos de reproducción y capa propietaria;
- lista de decisiones ya confirmadas y cuestiones aún abiertas.

Criterio de cierre: cada observación tiene evidencia, propietario y prioridad;
no hay propuestas basadas únicamente en impresión visual.

### A1. Propiedad de datos y persistencia

Objetivo: terminar la limpieza de base de datos y responsabilidades antes de
seguir ampliando el producto.

Se revisará:

- quién es propietario de cada tabla y documento persistente;
- qué rutas siguen dependiendo de una fachada general cuando ya existe un
  propietario más preciso;
- si alguna lectura, apertura o validación puede escribir accidentalmente;
- si quedan valores plausibles creados como fallback;
- si las Variants, referencias, Runtime Inputs y animaciones se consumen como
  documentos completos y actuales;
- si Usage, eliminación y navegación se alimentan de referencias explícitas;
- si datos, assets, fuentes e Icon Themes mantienen paridad entre equipos.

Entregables:

- matriz de propiedad de datos;
- lista cerrada de responsabilidades pendientes de separar;
- orden de limpieza por riesgo, empezando por lecturas antes que escrituras;
- identificación explícita de cualquier migración necesaria.

Criterio de cierre: la apertura permanece inmutable, no hay reparaciones
silenciosas y toda persistencia tiene un propietario inequívoco.

### A2. Fronteras funcionales hasta Preview

Objetivo: comprobar que cada decisión se toma una sola vez y en la capa que
conoce su significado.

Se trazará de extremo a extremo:

- edición de un campo y persistencia;
- selección y resolución de Variant;
- Component embebido, Override y Forward;
- Runtime Input y colección estructurada;
- Module Variant y Module Instance;
- Shot, Screen y contexto de Preview;
- ownership temporal recursivo y resolución frame a frame;
- entrega final al renderer.

Para cada recorrido se documentará:

- autoridad de origen;
- fronteras cruzadas;
- datos que deben permanecer completos;
- capa que decide y capa que solo transporta;
- duplicaciones, reconstrucciones o conocimientos indebidos.

Entregables:

- mapa de fronteras actualizado;
- lista de violaciones reales o riesgos verificables;
- propuesta de refactors independientes y reversibles.

Criterio de cierre: el shell coordina, los propietarios resuelven y Preview
recibe valores finales sin reconstruir el modelo.

### A3. Auditoría UX del sistema de Diseño

Objetivo: hacer eficiente la definición de recursos, Atoms, Components,
Variants, Apps y Modules sin ocultar sus contratos.

Casos obligatorios:

- cambiar un campo de una Variant de Label;
- cambiar entre Variants conservando card, sección y scroll;
- duplicar, renombrar, bloquear y revisar la eliminación de una Variant usada;
- abrir un Component embebido, aplicar un Override y volver por breadcrumb;
- activar y retirar Forward con su impacto visible;
- editar una colección estructurada y una colección de estructura fija;
- configurar Slot, State, Replace, Overlay y Reflow;
- diferenciar Test Values de valores reutilizables de Variant;
- usar Play y Restore en acciones de prueba;
- revisar Theme, Actor, Device, wallpaper, fuentes, iconos y assets;
- comprobar Usage y enlaces desde confirmaciones destructivas.

Preguntas de evaluación:

- ¿se entiende qué entidad se modifica antes de tocar un control?;
- ¿Variant, Override, Forward y Test Value se distinguen visualmente?;
- ¿las tareas habituales requieren demasiados cambios de card o contexto?;
- ¿hay acciones duplicadas con distinto aspecto o ubicación?;
- ¿el vocabulario de producto evita términos internos?;
- ¿el editor conserva el punto de trabajo al investigar una dependencia?;
- ¿la interfaz compacta sigue siendo comprensible?

Entregables:

- recorrido ideal para un usuario nuevo;
- recorrido compacto para un usuario experto;
- problemas de presentación separados de problemas del modelo;
- propuesta de jerarquía, cards, tabs, breadcrumbs y acciones.

Criterio de cierre: el flujo común es claro sin introducir otro editor ni
eliminar acceso a composición, forwarding, overrides o animación.

### A4. Auditoría UX de Producción

Objetivo: optimizar el montaje de Shots a partir de recursos ya definidos, sin
mezclarlo con el trabajo de Diseño.

Casos obligatorios:

- crear y recorrer Episode y Shot;
- cambiar el Actor propietario sin permitir un Shot sin Actor;
- seleccionar un Shot y una Screen y verificar el contexto de Preview;
- añadir y ordenar Module Instances;
- elegir una Module Variant de forma explícita;
- editar el payload persistente de una Screen;
- comparar ese payload con los Test Values del mismo Module en Diseño;
- editar Component Stack, Collection Stack y States desde su alcance real;
- activar animación, crear KF0, añadir y arrastrar keyframes;
- usar el mismo playhead en Animation y Preview;
- revisar duración calculada frente a duración explícita;
- revisar Usage y confirmaciones antes de cambios destructivos.

Preguntas de evaluación:

- ¿el usuario sabe siempre qué Shot y Screen está viendo?;
- ¿el árbol es suficiente como fuente única de contexto?;
- ¿el payload real se reconoce como dato de Producción?;
- ¿las operaciones frecuentes están agrupadas según el montaje del Shot?;
- ¿la animación expresa tiempo local sin obligar a entender su almacenamiento?;
- ¿volver desde Diseño conserva el lugar exacto de Producción?

Entregables:

- flujo ideal de montaje y revisión de un Shot;
- mapa de información necesaria en árbol, editor y Preview;
- lista de fricciones por frecuencia e impacto;
- propuesta para usuario nuevo y experto.

Criterio de cierre: montar una secuencia de Screens y sus payloads no requiere
comprender la implementación del sistema de Diseño, aunque todas las referencias
sigan siendo explícitas.

### A5. Navegación, contexto y lenguaje transversal

Objetivo: eliminar pérdidas de orientación y contradicciones entre superficies.

Se revisará:

- cambio entre Diseño y Producción;
- apertura desde Usage y desde confirmaciones destructivas;
- selección de árbol, editor activo y Preview resultante;
- cards, tabs, breadcrumbs, scroll y navegación embebida;
- memoria de sesión por clase de editor;
- responsive layout, splitters y paneles compactos;
- estados heredado, sobrescrito, forwarded, bloqueado, usado y animado;
- consistencia de nombres: Variant, Runtime Value, Test Value, Screen, Slot y
  State;
- redundancia entre títulos, cabeceras, contexto y breadcrumbs.

Entregables:

- glosario corto de producto;
- reglas de navegación y retorno;
- jerarquía de información recomendada;
- inventario de interacciones duplicadas que pueden unificarse.

Criterio de cierre: toda navegación abre workspace, árbol, elemento, editor,
subcontexto y Preview coherentes, y el retorno conserva el punto anterior.

### A6. Síntesis y priorización

Objetivo: convertir la auditoría en decisiones ejecutables y comparables.

Cada propuesta incluirá:

- problema observado y evidencia;
- capa propietaria;
- usuario y frecuencia afectados;
- impacto esperado;
- riesgo para contratos o datos;
- cambio de presentación o refactor arquitectónico;
- invariantes que lo protegen;
- comprobación manual de aceptación;
- decisiones que requieren aprobación.

Orden de prioridad:

1. integridad de datos y violaciones de fronteras;
2. funcionalidad incompleta que impide un flujo real;
3. pérdida de contexto o edición de la entidad equivocada;
4. duplicaciones y terminología que inducen errores;
5. mejoras de velocidad para tareas frecuentes;
6. refinamiento visual sin efecto funcional.

El riesgo se clasificará como:

- bajo: presentación o texto sin cambiar propiedad ni persistencia;
- medio: interacción compartida o navegación con contratos intactos;
- alto: propiedad, esquema, referencias, timing o resolución.

Entregables:

- backlog ordenado por impacto y riesgo;
- quick wins de presentación;
- refactors arquitectónicos independientes;
- decisiones aprobadas, rechazadas y aplazadas;
- plan de ejecución por olas.

### A7. Olas de mejora

Las mejoras aprobadas se entregarán en unidades pequeñas y coherentes:

1. lenguaje y presentación;
2. navegación y conservación de contexto;
3. unificación de interacciones compartidas;
4. limpieza de propietarios y fronteras;
5. migraciones explícitas, solo cuando sean imprescindibles.

Cada ola requiere diseño confirmado antes de implementarse, validación completa,
prueba de la aplicación real, documentación de la nueva regla, commit propio y
aprobación antes del push salvo autorización expresa previa.

## 5. Gate de baseline arquitectónico limpio

No se considerará terminada la limpieza por haber reducido el número de clases
o archivos. El baseline estará limpio cuando:

- todas las tablas y documentos tengan un propietario declarado;
- el shell no contenga comportamiento específico de un editor;
- no exista una segunda ruta para editar un mismo tipo de valor;
- no haya defaults o reparaciones ocultas para datos actuales incompletos;
- referencias, forwarding, overrides y Variants sean siempre explícitos;
- resolver, bridge y renderer mantengan sus fronteras;
- la animación use un único modelo temporal recursivo;
- las migraciones pendientes estén completadas o aprobadas como trabajo futuro;
- el registro de auditoría no contenga hallazgos críticos o altos sin decisión;
- las pruebas, validación de base de datos y revisión manual sean satisfactorias;
- el usuario apruebe este baseline como punto estable.

Los hallazgos menores aceptados pueden permanecer, pero deben conservar dueño,
prioridad y motivo de aplazamiento.

## 6. Línea B — procedimiento estricto de scaffolding con Codex

### 6.1 Alcance

Scaffolding significa preparar de forma repetible el paquete completo necesario
para que una nueva definición sea válida desde su creación. No significa generar
una fila parcial, copiar el último ejemplo ni añadir un botón genérico a la
aplicación.

El proceso seguirá siendo externo y dirigido por Codex. Una futura integración
de IA podrá guiar la conversación y mostrar propuestas, pero deberá usar el
mismo procedimiento y las mismas aprobaciones.

### 6.2 Clasificación inicial

Antes de proponer nada, Codex clasificará la petición:

- **Atom**: primitiva visual o de comportamiento de bajo nivel, reutilizable y
  sin conocimiento del dominio que la utiliza.
- **Component**: unidad visual reutilizable con significado propio, posibles
  hijos embebidos, Variants y Runtime Inputs.
- **Module**: composición reutilizable de una Screen, con contrato público,
  Module Variants, duración y comportamiento temporal.

Si la petición encaja en una definición existente, Codex propondrá extenderla o
crear una Variant antes de crear otra identidad. La coincidencia se demostrará
por contrato y comportamiento, nunca solo por nombre o aspecto.

### 6.3 Ficha de entrada obligatoria

La descripción del usuario puede ser informal. Codex la convertirá en una ficha
con estos apartados:

1. objetivo y caso de uso;
2. tipo propuesto: Atom, Component o Module;
3. nombre visible y propuesta separada de id estable;
4. referencias visuales o de comportamiento;
5. partes fijas y partes configurables;
6. Variants iniciales necesarias;
7. valores que pertenecen a Diseño;
8. valores que deben llegar en Runtime;
9. colecciones, acciones y estados;
10. Components embebidos y relaciones entre ellos;
11. comportamiento temporal, duración y animación;
12. Theme tokens, fuentes, iconos, imágenes u otros assets;
13. estados vacíos, límites y errores que deben ser visibles;
14. escenarios mínimos de Preview y aceptación;
15. aspectos expresamente fuera de alcance.

Codex investigará el catálogo actual para completar lo que pueda demostrar. Solo
preguntará por decisiones de producto que cambien realmente el resultado.

### 6.4 Primera entrega: brief de definición

Antes de crear o modificar archivos, Codex presentará un brief no técnico con:

- interpretación del objetivo;
- decisión razonada entre reutilizar, extender o crear;
- clasificación Atom, Component o Module;
- propietario de cada grupo de valores;
- lista de Variants y contenido completo de Default;
- Runtime Inputs públicos y sus valores iniciales;
- estructura de colecciones, Slots, States y acciones;
- hijos embebidos seleccionados mediante Variants concretas;
- Forwarding propuesto en cada frontera;
- Overrides locales permitidos;
- política de duración y ownership temporal;
- comportamiento esperado en Preview para frames representativos;
- assets y dependencias;
- experiencia prevista en el editor;
- riesgos, alternativas y decisiones abiertas.

El usuario debe confirmar este brief. Una descripción imperativa no sustituye
esta aprobación de diseño.

### 6.5 Reglas para resolver ambigüedades

Codex debe detenerse y preguntar cuando no esté claro:

- quién es propietario de un valor;
- si un valor es de Variant o Runtime;
- si un hijo debe estar fijo, ser seleccionable o estar forwarded;
- si una colección es estructuralmente fija o editable;
- qué Variant concreta cruza una frontera;
- si una acción es instantánea, finita o secuenciadora;
- si la duración de un Module es calculada o explícita;
- si dos estados representan Variants, States o valores animados;
- si un nuevo recurso sustituye o duplica uno existente;
- si una migración puede alterar datos ya utilizados.

No debe resolver estas dudas usando nombres, tipos, orden, plataforma, Actor,
Device, Theme o posición como inferencia automática.

### 6.6 Paquete de definición aprobado

Tras la aprobación, Codex preparará un inventario completo de la entrega. Según
el tipo, incluirá:

- identidad estable y categoría explícita;
- definición actual completa y Default Variant protegida;
- Variants adicionales aprobadas como snapshots completos;
- declaración de campos editables y su organización;
- contrato de Runtime Inputs, colecciones, acciones y forwarding;
- composición mediante referencias completas y Overrides locales;
- propietario de resolución y presentación en Preview;
- tratamiento temporal y de duración;
- assets y recursos requeridos;
- incorporación explícita a los datos actuales;
- documentación de comportamiento;
- pruebas de contrato, Preview, persistencia y experiencia de editor.

Nada se promoverá como definición actual si falta una de sus partes aplicables.
Los ejemplos y plantillas pertenecen al proceso de desarrollo; no se convertirán
en defaults alternativos dentro de la aplicación.

### 6.7 Diferencias por tipo

#### Atom

Debe demostrar que:

- es una primitiva reutilizable y no una excepción de un Module;
- sus valores configurables tienen significado general;
- no requiere que el renderer conozca su nombre;
- Default funciona de forma aislada;
- cualquier Runtime Input tiene utilidad fuera de un único ejemplo.

#### Component

Debe demostrar que:

- tiene una responsabilidad visual o funcional clara;
- sus hijos embebidos y sus fronteras están declarados;
- cada Variant es completa;
- Runtime, Forward y Override son intencionados;
- los estados vacíos y combinaciones válidas están definidos;
- Preview aislado y Preview embebido producen el mismo significado.

#### Module

Debe demostrar que:

- representa una composición reutilizable de Screen;
- la diferencia entre Module Variant y payload de Module Instance es clara;
- su contrato público contiene solo valores realmente necesarios en Producción;
- el forwarding recursivo es explícito;
- duración, secuenciación y ownership temporal están declarados;
- puede resolverse en una Screen y dentro de un Shot completo.

### 6.8 Gates de aprobación

El procedimiento usa cinco gates:

1. **Necesidad** — se aprueba crear o extender una definición.
2. **Contrato** — se aprueban propiedad, Variants, Runtime y composición.
3. **UX** — se aprueba cómo se editará y comprenderá.
4. **Ejecución** — el usuario autoriza implementar el paquete completo.
5. **Aceptación** — se revisan aplicación real, Preview y escenarios acordados.

Un gate rechazado devuelve el proceso al brief; no se conserva una definición
parcial como fallback ni como supuesto trabajo terminado.

### 6.9 Secuencia de ejecución futura

Cuando el usuario autorice una creación:

1. verificar baseline y que no existe otro hilo escribiendo en el proyecto;
2. releer contratos aplicables y el brief aprobado;
3. preparar la definición completa en una sola fase coherente;
4. incorporar datos y assets mediante el flujo explícito correspondiente;
5. validar fronteras, persistencia, resolución, Preview y editor;
6. abrir la aplicación y recorrer los escenarios de aceptación;
7. corregir cualquier desviación antes de cerrar;
8. documentar la nueva definición y sus decisiones;
9. crear un commit coherente y esperar autorización de push, salvo que ya se
   haya concedido expresamente.

La creación no se ejecutará al abrir la aplicación y no se ofrecerá como Add
Atom, Add Component o Add Module en la interfaz normal.

### 6.10 Definición de terminado

Una nueva definición solo está terminada cuando:

- el brief aprobado y el resultado coinciden;
- identidad, Default y Variants están completos;
- los valores aparecen en el propietario correcto;
- no existen inferencias o defaults ocultos;
- Runtime Inputs, forwarding y Overrides funcionan en todas sus fronteras;
- el tiempo se resuelve desde el owner correcto;
- Preview aislado, embebido y de Producción coinciden cuando sean aplicables;
- Usage y protecciones destructivas reconocen las nuevas referencias;
- el editor usa patrones compartidos y funciona en panel compacto;
- datos y assets son portables entre equipos;
- las validaciones automáticas y los casos manuales pasan;
- la documentación impide reintroducir una ruta alternativa.

## 7. Plantilla para futuras peticiones del usuario

El usuario puede iniciar el proceso con una descripción libre o con esta
plantilla abreviada:

```text
Quiero crear: [Atom / Component / Module / no estoy seguro]
Objetivo:
Aspecto o referencia:
Qué debe poder configurar una Variant:
Qué debe cambiar en Runtime:
Estados, acciones o colecciones:
Components que debería reutilizar:
Comportamiento temporal:
Assets necesarios:
Escenarios que deben funcionar:
Fuera de alcance:
```

Codex responderá primero con el brief de la sección 6.4, no con código.

## 8. Registro de decisiones y trazabilidad

Cada fase de limpieza y cada nueva definición conservarán:

- objetivo aprobado;
- evidencia de partida;
- decisiones del usuario;
- invariantes protegidos;
- comprobaciones realizadas;
- resultado y commit final;
- deuda aceptada o siguiente fase.

Los documentos normativos describen reglas duraderas. El registro de auditoría
describe hechos observados. Ninguno debe convertirse en una copia del código.

## 9. Trabajo futuro fuera de este plan inmediato

- integración conversacional de IA dentro de la aplicación;
- generación asistida de briefs desde imágenes o referencias externas;
- duplicación de Projects con selección entre copiar, regenerar o vaciar cada
  categoría;
- Render Mode, cola de renders y export final;
- transitions distintas de Cut;
- aprobación, versiones y empaquetado final de Producción.

Estas áreas podrán apoyarse en el baseline y el procedimiento de scaffolding,
pero requieren decisiones propias antes de entrar en implementación.

## 10. Resultado esperado

Al completar este plan, MOCKUPS tendrá:

- una arquitectura sin propietarios ambiguos ni reparación silenciosa;
- una UX coherente con la separación real entre Diseño y Producción;
- un flujo claro para usuarios nuevos y rápido para trabajo experto;
- un backlog de mejoras trazable por impacto, riesgo y capa;
- un único procedimiento para convertir una descripción en una definición
  completa de Atom, Component o Module;
- documentación y gates suficientes para que Codex no repita decisiones ni
  introduzca atajos incompatibles en futuras sesiones.
