# City3D

`citygen.py` mirrors `Assets/Editor/City/LowPolyCityGenerator.cs` to preview the procedural low-poly city outside
Unity. Needs Python 3 with numpy and Pillow.

    python Tools/City3D/citygen.py . Data/Previews/City3D/preview.png

Prints the triangle count per mesh group and the landmark IDs, and writes an isometric render. See `Data/City3D.md`.
