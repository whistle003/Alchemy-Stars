# Third-party notices

## Alchemist

Alchemy Stars is based on [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist),
which is licensed under GPL-3.0. The full source and license are provided in
`fork/AlchemyStars`.

## RedFox and Cast.NET

The original Alchemist pipeline depends on Scobalula/RedFox and Cast.NET. Their
pinned source trees and license files are retained in the `fork/RedFox` Git
submodule.

## Maya CAST plugin

Alchemy Stars redistributes the Python CAST format library and Autodesk Maya
file translator from [dtzxporter/cast](https://github.com/dtzxporter/cast).
Those files are licensed under the MIT License; the original license text is
stored at `third_party/cast/LICENSE`.

Alchemy Stars physically combines viewhands, weapon, and attachment data into
one model node with one skeleton before writing CAST output. The plugin's
`importMerge` option is therefore not required for importing into a new scene;
it remains available for intentionally merging into an existing scene skeleton.
