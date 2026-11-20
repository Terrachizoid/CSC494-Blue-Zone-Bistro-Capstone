# City3D

`citygen.py` is the city generator (Unity only imports its output); `cityparts.py` holds the shapes and the
building/tree library. Python 3; `preview` also needs numpy and Pillow.

    python Tools/City3D/citygen.py build .                 # plan if no lots table yet, then build meshes
    python Tools/City3D/citygen.py plan . --reroll         # new random plan (overwrites the lots table)
    python Tools/City3D/citygen.py preview . out.png       # render without Unity (--close for a close-up)

The lots table (Assets/GameData/City/Blue_Zone_Bistro_CityLots.csv) is the frozen city: edit rows, then build.
Then in Unity: Tools > Blue Zone Bistro > Generate Low-Poly City. See `Data/City3D.md`.
