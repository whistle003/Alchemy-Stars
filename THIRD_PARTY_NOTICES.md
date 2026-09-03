# Third-party notices

## Cast

Alchemy Stars redistributes the Python CAST format library and Autodesk Maya
file translator from [dtzxporter/cast](https://github.com/dtzxporter/cast).
Those files are licensed under the MIT License; the original license text is
stored at `third_party/cast/LICENSE`.

The bundled `castplugin.py` is modified only to enable `importMerge` by
default. Alchemy Stars output contains viewhands and weapon as separate model
roots sharing `j_gun`; enabling this option makes a single file import merge
them into one Maya skeleton before applying the baked animation.

