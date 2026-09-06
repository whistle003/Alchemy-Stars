Source: https://github.com/dtzxporter/cast
Commit: a8ca18a0acf3b97b19332c53b54b47fcc3217755
License: MIT (../LICENSE)

Files: plugins/blender/*.py and libraries/python/cast.py.
Local adaptation: a complete single-model CAST scene binds its animation to the newly
imported armature, including when the scene was empty or a mesh was selected.
Joint display length is 1.0 instead of 0.0025 source units to reduce loss of
orientation precision at large float32 coordinates. This does not change joint
head positions or animation transforms. Other plugin logic remains unchanged.
Alchemy Stars uses baked curves with
imported IK and constraints disabled to avoid evaluating solvers a second time.
