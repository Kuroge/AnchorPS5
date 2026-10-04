# Novedades

**Español** · [English](CHANGELOG.en.md)

Todos los cambios importantes de AnchorPS5. El formato sigue
[Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y las versiones siguen
[SemVer](https://semver.org/lang/es/). Mientras la app sea **alpha**, puede cambiar mucho
de una versión a otra.

## [0.1.1-alpha.1] — 2026-10-04

### Añadido

- **Borrar versiones anteriores al actualizar:** cualquier forma de actualizar (un fichero,
  «Actualizar todo» de una app o de la sección Actualizaciones) pregunta si quieres borrar las
  versiones anteriores o conservarlas, con «No volver a preguntar» (conservarlas siempre). Lo
  anterior solo se borra cuando la versión nueva está descargada y comprobada; si algo falla,
  se conserva.
- **Descargar todo:** en las apps con varios ficheros, el menú «Descargar» empieza por
  «Descargar todo», que baja de una vez los ficheros que aún no tienes.
- **Fecha de cada versión:** junto a cada versión se muestra cuándo se publicó (en las
  etiquetas de versión de la cabecera de la ficha, los ficheros, la beta disponible, el
  historial de versiones y la tarjeta de Información).
- **Botón ⓘ en cada fichero descargado y en cada versión de su historial:** versión, canal
  (estable o beta), fecha de publicación, fecha y hora de descarga, tamaño en disco, si se
  comprobó el SHA-256, el propio SHA-256, de dónde se descargó y dónde está guardado.

### Cambiado

- **Descargar y actualizar, por separado:** en las apps con varios ficheros, «Descargar» solo
  descarga lo que no tienes (lo que ya tienes sale deshabilitado, aunque esté desactualizado)
  y «Actualizar todo (N)» es un botón partido: al pulsarlo actualiza todo y con su flecha
  eliges fichero a fichero (con la versión que tienes y la nueva).
- En **Información**, la versión (y la versión beta) enlaza directamente a su release en
  GitHub; desaparece la fila "Último release", que repetía lo mismo.
- La lista de ficheros de la ficha se titula solo **Archivos**: cada fichero ya indica su
  propia versión (antes el título mostraba la última versión publicada y confundía).

## [0.1.0-alpha.2] — 2026-10-02

### Cambiado

- El botón Atrás de la barra superior pasa a ser **Inicio** (🏠): vuelve al catálogo desde
  cualquier pantalla (Acerca de, una ficha, otra sección) y solo aparece cuando hace falta.
- En la configuración inicial, **cambiar el idioma traduce la pantalla al momento**.
- **Examinar…** (carpeta de descargas) abre directamente en la carpeta Descargas.

## [0.1.0-alpha.1] — 2026-10-02

Primera versión de prueba.

### Añadido

- **Catálogo de homebrew** en tarjetas, con búsqueda, orden (por nombre o novedades) y
  secciones: Catálogo, Descargadas, Actualizaciones y Nuevas (apps que no habías visto).
- **Catálogo oficial** que se descarga del repo `Kuroge/AnchorPS5-catalog` y funciona sin
  conexión con la última copia. Puedes **añadir tus propias apps**: cuando llega una
  versión oficial nueva, la app te pregunta si conservarlas o sustituirlo todo.
- **Origen de cada app:** catálogo oficial, añadida por ti u otro catálogo.
- **Ficheros siempre al día desde GitHub:** cada app muestra los ficheros de su última
  release (con su SHA-256), con etiqueta **PS4** cuando corresponde.
- **Descargas** con progreso, cola, verificación SHA-256 y extracción automática con
  7-Zip incluido. Historial de versiones por fichero, con abrir carpeta y borrar.
- **Actualizaciones** por fichero (`0.1 → 0.2`) y botón **Actualizar todo**.
- **Betas** marcadas con un matraz 🧪: se pueden probar por fichero y volver a la estable
  cuando quieras (conservando o borrando la beta).
- **Inicio de sesión con GitHub (opcional)** para subir el límite de consultas de 60 a
  5000 por hora, con tu foto y perfil en la barra superior.
- **Sin depender del límite de GitHub:** el catálogo oficial publica cada hora un índice
  con los ficheros de todas sus apps, y sin sesión la app lo descarga de una vez en lugar
  de preguntar app por app. Con sesión se pregunta a GitHub directamente (datos más al
  día) y el índice queda de respaldo. Para tus apps propias: caché de 30 minutos, refresco automático
  en segundo plano y espera automática si se alcanza el límite.
- **Botón Recargar** para ver al momento una versión recién publicada (sin sesión, avisa
  antes de que pueda gastar el límite de GitHub).
- **Actualizaciones de la app:** al arrancar (y desde **Acerca de → Buscar
  actualizaciones**) avisa de las versiones nuevas con sus novedades; **Actualizar ahora**
  la descarga, comprueba su SHA-256 y la instala reiniciando la app, sin tocar tu
  configuración ni tus descargas.
- **Acerca de:** versión, licencia, enlaces, componentes de terceros, créditos y acceso al
  registro de la app (`config/logs`) para reportar fallos.
- **Más robusta:** un `config.json` o un idioma mal escritos ya no cierran la app (se avisa
  y se aparta una copia del fichero).
- Botón **Atrás** dentro de la ficha de cada app.
- **Configuración inicial:** idioma, carpeta de descargas y cuenta de GitHub.
- **Textos traducibles** desde ficheros JSON (`lang/`), también las descripciones del
  catálogo. De fábrica, en **español e inglés**; el idioma se elige en la configuración inicial, con
  el de Windows preseleccionado.
- **Diseño** con los colores de PlayStation y el dorado del arranque de PS5, fondo Mica y
  tema claro u oscuro según Windows.

[0.1.1-alpha.1]: https://github.com/Kuroge/AnchorPS5/releases/tag/v0.1.1-alpha.1
[0.1.0-alpha.2]: https://github.com/Kuroge/AnchorPS5/releases/tag/v0.1.0-alpha.2
[0.1.0-alpha.1]: https://github.com/Kuroge/AnchorPS5/releases/tag/v0.1.0-alpha.1
