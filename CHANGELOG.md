# Novedades

**Español** · [English](CHANGELOG.en.md)

Todos los cambios importantes de AnchorPS5. El formato sigue
[Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y las versiones siguen
[SemVer](https://semver.org/lang/es/). Mientras la app sea **alpha**, puede cambiar mucho
de una versión a otra.

## [0.1.0-alpha.1] — Sin publicar

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
- **Consultas a GitHub eficientes:** caché de 30 minutos, refresco automático en segundo
  plano y espera automática si se alcanza el límite.
- **Configuración inicial:** idioma, carpeta de descargas y cuenta de GitHub.
- **Textos traducibles** desde ficheros JSON (`lang/`), también las descripciones del
  catálogo. De fábrica, en español.
- **Diseño** con los colores de PlayStation y el dorado del arranque de PS5, fondo Mica y
  tema claro u oscuro según Windows.

[0.1.0-alpha.1]: https://github.com/Kuroge/AnchorPS5/releases/tag/v0.1.0-alpha.1
