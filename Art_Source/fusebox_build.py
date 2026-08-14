"""
AI Tycoon — Hauptsicherungskasten mit Lastsaeule.
Generator-Helfer fuer Blender 5.2.

Konvention:
  - 1 Blender-Einheit = 1 m = 1 Unity-Unit
  - Front zeigt nach -Y, oben ist +Z
  - Rueckseite (Wandkontaktebene) liegt bei Y = 0, alles ragt nach -Y
  - Unterkante der Baugruppe bei Z = 0, horizontal zentriert um X = 0

Alle Bau-Funktionen sind idempotent: gleichnamige Objekte werden zuerst geloescht.
"""

import bpy
import bmesh
from mathutils import Vector, Matrix

# ---------------------------------------------------------------- CONFIG ----

CONFIG = {
    # Gesamt-Baugruppe
    "total_w": 1.16,
    "total_h": 1.45,
    "total_d": 0.32,

    # Rueckplatte
    "plate": dict(x0=-0.58, x1=0.58, y0=-0.05, y1=0.00, z0=0.00, z1=1.45),

    # --- Kasten (links) ---------------------------------------------------
    # Korpus als Multibox: Rueckslab + vier Waende -> echte Nische hinter dem
    # Fenster, damit der Sicherungsblock sichtbar ist (kein Boolean noetig).
    "box_outer": dict(x0=-0.52, x1=0.10, z0=0.28, z1=1.20),
    "box_y_back": -0.05, "box_y_slab": -0.14, "box_y_front": -0.32,
    "pocket":    dict(x0=-0.44, x1=0.02, z0=0.76, z1=1.14),

    # Tuer = Fensterrahmen, Scharnier an der LINKEN Kante
    "door":      dict(x0=-0.475, x1=0.055, z0=0.715, z1=1.185, y0=-0.345, y1=-0.320, t=0.045),
    "door_hinge": (-0.475, -0.3325, 0.950),
    "glass":     dict(x0=-0.430, x1=0.010, y0=-0.338, y1=-0.327, z0=0.760, z1=1.140),

    # --- Hauptsicherung ---------------------------------------------------
    # Sicherungsblock in der Nische, durch das Fenster sichtbar
    "breaker":   dict(x0=-0.35, x1=-0.07, y0=-0.26, y1=-0.15, z0=0.80, z1=1.10),
    # Dunkle Nischenrueckwand — sonst verliert der Sicherungsblock im hellen
    # Gehaeuseinneren jeden Kontrast.
    "niche_back": dict(x0=-0.44, x1=0.02, y0=-0.152, y1=-0.140, z0=0.76, z1=1.14),
    # Schaltertafel unterhalb des Fensters, auf der Kastenfront
    "lev_plate": dict(x0=-0.45, x1=0.03, y0=-0.340, y1=-0.320, z0=0.320, z1=0.690),
    # Drehpunkt des Riesenhebels (Rotation um die Y-Achse = flach vor der Front)
    "lever_pivot": (-0.21, -0.360, 0.470),
    "lever_arm":  dict(x0=-0.255, x1=-0.165, y0=-0.405, y1=-0.360, z0=0.470, z1=0.655),
    "lever_knob": dict(x0=-0.295, x1=-0.125, y0=-0.425, y1=-0.350, z0=0.630, z1=0.700),
    "lever_boss": dict(x0=-0.280, x1=-0.140, y0=-0.368, y1=-0.320, z0=0.400, z1=0.540),
    # 135 Grad: gerade so viel Schwenk, dass die Hebelspitze in der Schaltzone
    # bleibt (Kasten-Unterkante 0.28) — geprueft mit lever_sweep_bounds().
    "lever_blowout_deg": 135.0,

    # Lastsaeule (rechts) — als U-Profil gebaut, damit die Nut echte Geometrie ist
    # (kein Boolean noetig): Rueckslab + zwei Seitenschienen + Fuss + Kappe.
    "col_slab":  dict(x0=0.20, x1=0.52,  y0=-0.215, y1=-0.05, z0=0.08, z1=1.37),
    "col_railL": dict(x0=0.20, x1=0.248, y0=-0.260, y1=-0.215, z0=0.14, z1=1.30),
    "col_railR": dict(x0=0.472, x1=0.52, y0=-0.260, y1=-0.215, z0=0.14, z1=1.30),
    "col_base":  dict(x0=0.16, x1=0.56,  y0=-0.295, y1=-0.05, z0=0.02, z1=0.14),
    "col_cap":   dict(x0=0.16, x1=0.56,  y0=-0.295, y1=-0.05, z0=1.30, z1=1.44),

    # Segmentstapel in der Nut
    "seg_count": 12,
    "seg_x0": 0.252, "seg_x1": 0.468,
    "seg_z0": 0.185, "seg_z1": 1.275,
    "seg_gap": 0.018,
    "seg_y0": -0.255, "seg_y1": -0.215,   # ragt aus der Nut nach vorn

    # Farbbaender (identisch zur HUD-Logik: t=(i+0.5)/count gegen 0.60 / 0.85)
    "warn_threshold": 0.60,
    "critical_threshold": 0.85,

    # Kanten
    "bevel_width": 0.010,
    "bevel_segments": 2,

    # Gradient-UV: V = normierte Hoehe, auf diesen Bereich remapped
    "uv_v_min": 0.18,
    "uv_v_max": 0.98,
}

COLLECTION = "FuseBox"

# Pfad zu den nappin-Gradient-Texturen (nur fuer die Blender-Vorschau)
NAPPIN_TEX = ("/Users/tobias/develop/ai-tycoon/Assets/ThirdParty/nappin/"
              "OfficeEssentialsPack/Textures/(Txt)Gradient{}.png")


def seg_band(i, count=None):
    """Farbband eines Segments nach Position — dieselbe Formel wie SystemLoadBarUI."""
    count = count or CONFIG["seg_count"]
    t = (i + 0.5) / count
    if t < CONFIG["warn_threshold"]:
        return "green"
    if t < CONFIG["critical_threshold"]:
        return "yellow"
    return "red"


# --------------------------------------------------------------- HELPERS ----

def purge(name):
    """Objekt (und sein Mesh) restlos entfernen."""
    obj = bpy.data.objects.get(name)
    if obj is None:
        return
    data = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if isinstance(data, bpy.types.Mesh) and data.users == 0:
        bpy.data.meshes.remove(data)


def _collection(name=COLLECTION):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    return col


def link(obj, collection=COLLECTION):
    """Objekt exklusiv in die angegebene Collection haengen."""
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    _collection(collection).objects.link(obj)
    return obj


def make_box(name, x0, x1, y0, y1, z0, z1, origin=None, collection=COLLECTION):
    """Quader aus Weltkoordinaten-Grenzen. origin = Weltpunkt fuer den Objekt-Ursprung."""
    purge(name)
    ox, oy, oz = origin if origin else ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)

    verts = [(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    verts = [(v[0] - ox, v[1] - oy, v[2] - oz) for v in verts]
    # Index: (x,y,z) -> x*4 + y*2 + z
    faces = [
        (0, 1, 3, 2),  # x0
        (4, 6, 7, 5),  # x1
        (0, 2, 6, 4),  # y0  (Front, zeigt nach -Y)
        (1, 5, 7, 3),  # y1  (Rueckseite)
        (0, 4, 5, 1),  # z0
        (2, 3, 7, 6),  # z1
    ]

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    mesh.validate()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = (ox, oy, oz)
    link(obj, collection)
    shade_flat(obj)
    return obj


def _box_into_bm(bm, x0, x1, y0, y1, z0, z1):
    v = [bm.verts.new((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    for f in [(0,1,3,2), (4,6,7,5), (0,2,6,4), (1,5,7,3), (0,4,5,1), (2,3,7,6)]:
        bm.faces.new([v[i] for i in f])


def make_multibox(name, boxes, origin=(0, 0, 0), collection=COLLECTION):
    """Mehrere Quader als EIN Objekt (z.B. Rahmen aus vier Leisten)."""
    purge(name)
    bm = bmesh.new()
    for b in boxes:
        _box_into_bm(bm, **b)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    link(obj, collection)
    shade_flat(obj)
    set_origin(obj, origin)
    return obj


def make_frame(name, x0, x1, z0, z1, y0, y1, t, origin=None, collection=COLLECTION):
    """Rechteckiger Rahmen (Ring) mit Leistenstaerke t."""
    boxes = [
        dict(x0=x0,     x1=x1,     y0=y0, y1=y1, z0=z1 - t, z1=z1),      # oben
        dict(x0=x0,     x1=x1,     y0=y0, y1=y1, z0=z0,     z1=z0 + t),  # unten
        dict(x0=x0,     x1=x0 + t, y0=y0, y1=y1, z0=z0 + t, z1=z1 - t),  # links
        dict(x0=x1 - t, x1=x1,     y0=y0, y1=y1, z0=z0 + t, z1=z1 - t),  # rechts
    ]
    o = origin if origin else ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    return make_multibox(name, boxes, origin=o, collection=collection)


def make_cylinder(name, radius, depth, location, axis='Z', verts=12,
                  origin=None, collection=COLLECTION):
    """Low-Poly-Zylinder. axis = Ausrichtung der Rotationsachse."""
    purge(name)
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=verts,
                          radius1=radius, radius2=radius, depth=depth)
    if axis == 'X':
        bmesh.ops.rotate(bm, verts=bm.verts,
                         matrix=Vector((0, 1, 0)).to_track_quat('Z', 'Y').to_matrix())
    elif axis == 'Y':
        bmesh.ops.rotate(bm, verts=bm.verts,
                         matrix=Vector((0, 0, 1)).to_track_quat('Y', 'Z').to_matrix())
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    link(obj, collection)
    shade_flat(obj)
    if origin:
        set_origin(obj, origin)
    return obj


def parent_to(child, parent):
    """Parenting ohne Sprung. Wichtig: view_layer.update() VOR dem Lesen von
    matrix_world — sonst ist die Matrix noch stale und das Kind versetzt sich."""
    bpy.context.view_layer.update()
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()
    bpy.context.view_layer.update()
    return child


def shade_flat(obj):
    for p in obj.data.polygons:
        p.use_smooth = False


def set_origin(obj, world_point):
    """Objekt-Ursprung auf einen Weltpunkt legen, Geometrie bleibt stehen."""
    delta = Vector(world_point) - obj.location
    obj.data.transform(Matrix.Translation(-delta))
    obj.location = Vector(world_point)
    obj.data.update()
    return obj


def bevel(obj, width=None, segments=None, clamp=True):
    """Bevel-Modifier (wird in Phase G angewandt)."""
    width = CONFIG["bevel_width"] if width is None else width
    segments = CONFIG["bevel_segments"] if segments is None else segments
    mod = obj.modifiers.get("Bevel")
    if mod is None:
        mod = obj.modifiers.new("Bevel", 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod.angle_limit = 0.523599          # 30 Grad
    mod.use_clamp_overlap = clamp
    mod.harden_normals = False          # flat shaded — keine Normalen-Tricks noetig
    return mod


# ------------------------------------------------------------- MATERIALS ----

def ensure_material(name, base_color, texture=None, emission=None,
                    metallic=0.0, roughness=0.6, alpha=1.0):
    """Material anlegen/aktualisieren. Name = exakt der Unity-Materialname."""
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (400, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (100, 0)
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    bsdf.inputs["Base Color"].default_value = (*base_color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if "Alpha" in bsdf.inputs:
        bsdf.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        mat.blend_method = 'BLEND'

    if texture:
        img = bpy.data.images.get(texture.split("/")[-1])
        if img is None:
            try:
                img = bpy.data.images.load(texture)
            except Exception as exc:
                print("Textur nicht ladbar:", texture, exc)
                img = None
        if img:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.location = (-300, 0)
            tex.image = img
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission[:3], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission[3]

    return mat


# Materialnamen = EXAKT die Unity-Materialnamen. Dadurch bindet Unitys
# FBX-Importer die vorhandenen nappin-Materialien beim Import von selbst.
# (tex = Dateiname-Teil der nappin-Gradient-Rampe, nur fuer die Blender-Vorschau)
# Basisfarben = gemessene Mittelwerte der nappin-Rampen (V=0.5). Die Rampen sind
# oberhalb V~0.30 flach und dunkeln nur nach unten ab — daher traegt das UV-Remap
# den Verlauf, nicht die Basisfarbe.
MATERIALS = {
    "(Mat)GradientGrey":      dict(base=(0.83, 0.83, 0.83), tex="Grey"),
    "(Mat)GradientDarkGrey":  dict(base=(0.61, 0.61, 0.61), tex="Darkgrey"),
    "(Mat)GradientBlack":     dict(base=(0.02, 0.02, 0.02), tex="Black"),
    "(Mat)GradientRed":       dict(base=(1.00, 0.00, 0.15), tex="Red"),
    "(Mat)GradientDarkRed":   dict(base=(0.68, 0.13, 0.05), tex="Darkred"),
    "(Mat)GradientYellow":    dict(base=(1.00, 0.87, 0.19), tex="Yellow"),
    "(Mat)MetallicBlack":     dict(base=(0.10, 0.10, 0.11), metallic=0.85, roughness=0.35),
    "(Mat)Glass":             dict(base=(0.62, 0.78, 0.84), roughness=0.05, alpha=0.11),
    # Segment-Materialien — Farbwerte 1:1 aus SystemLoadBarUI.cs:43-45.
    # Emission hier nur preview-tauglich; in Unity kommen HDR-Werte 3.5 / 5.0.
    "(Mat)FuseSegment_Off":    dict(base=(0.12, 0.12, 0.13), roughness=0.5),
    "(Mat)FuseSegment_Green":  dict(base=(0.30, 0.85, 0.35), emission=(0.30, 0.85, 0.35, 0.9)),
    "(Mat)FuseSegment_Yellow": dict(base=(0.95, 0.80, 0.25), emission=(0.95, 0.80, 0.25, 0.9)),
    "(Mat)FuseSegment_Red":    dict(base=(0.95, 0.30, 0.25), emission=(0.95, 0.30, 0.25, 1.3)),
}

# Bauteil -> Material. Leitidee: helles Gehaeuse, mittelgraue Rahmen,
# fast schwarzer Saeulenschacht (damit die Segmente maximal knallen),
# roter Hebel und gelbes Schild als die beiden Farbakzente.
ASSIGNMENT = {
    "Box_Backplate": "(Mat)GradientDarkGrey",
    "Box_Housing":   "(Mat)GradientGrey",
    "Box_Door":      "(Mat)GradientDarkGrey",
    "Box_DoorGlass": "(Mat)Glass",
    "Box_Handle":    "(Mat)MetallicBlack",
    "Breaker_Backboard": "(Mat)GradientBlack",
    "Breaker_Base":  "(Mat)GradientDarkRed",
    "Breaker_Plate": "(Mat)GradientDarkGrey",
    "Breaker_Boss":  "(Mat)MetallicBlack",
    "Breaker_Lever": "(Mat)GradientRed",
    "Column_Slab":   "(Mat)GradientBlack",
    "Column_RailL":  "(Mat)GradientDarkGrey",
    "Column_RailR":  "(Mat)GradientDarkGrey",
    "Column_Base":   "(Mat)GradientGrey",
    "Column_Cap":    "(Mat)GradientGrey",
    "Column_Ribs":   "(Mat)GradientBlack",
    "Conduit":       "(Mat)GradientDarkGrey",
    "Sign_Warning":  "(Mat)GradientYellow",
    "Screw_00": "(Mat)MetallicBlack", "Screw_01": "(Mat)MetallicBlack",
    "Screw_02": "(Mat)MetallicBlack", "Screw_03": "(Mat)MetallicBlack",
}

SEG_MAT = {
    "green":  "(Mat)FuseSegment_Green",
    "yellow": "(Mat)FuseSegment_Yellow",
    "red":    "(Mat)FuseSegment_Red",
}


def build_materials(with_textures=True):
    """E.1/E.2 — Materialien anlegen, Gradient-Rampen fuer die Vorschau laden."""
    made = []
    for name, spec in MATERIALS.items():
        tex = NAPPIN_TEX.format(spec["tex"]) if (with_textures and spec.get("tex")) else None
        ensure_material(
            name,
            base_color=spec["base"],
            texture=tex,
            emission=spec.get("emission"),
            metallic=spec.get("metallic", 0.0),
            roughness=spec.get("roughness", 0.6),
            alpha=spec.get("alpha", 1.0),
        )
        made.append(name)
    return made


def assign_materials():
    """E.3/E.4 — Material je Bauteil, Segmente nach Farbband."""
    missing = []
    for obj_name, mat_name in ASSIGNMENT.items():
        obj = bpy.data.objects.get(obj_name)
        if obj is None:
            missing.append(obj_name)
            continue
        assign_mat(obj, mat_name)

    # Segmente bekommen EIN gemeinsames, dunkles Material — nicht ihre Bandfarbe.
    # Gruende: (1) Unity legt sonst drei ueberfluessige .mat-Assets an, (2) ein
    # unbespieltes Prefab soll nicht wie Volllast aussehen, (3) die Farbe setzt
    # zur Laufzeit ohnehin LoadPillar. Bandfarben zeigt preview_load().
    for i in range(CONFIG["seg_count"]):
        obj = bpy.data.objects.get(f"Segment_{i:02d}")
        if obj is None:
            missing.append(f"Segment_{i:02d}")
            continue
        assign_mat(obj, "(Mat)FuseSegment_Off")
    return missing


def assign_mat(obj, mat_name):
    mat = bpy.data.materials.get(mat_name)
    if mat is None:
        raise KeyError("Material fehlt: " + mat_name)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


# --------------------------------------------------------------------- UV ----

def set_gradient_uv(obj, z_min=0.0, z_max=None, u=0.5):
    """V = normierte Welt-Z-Hoehe ueber die Gesamt-Baugruppe (nappin-Look)."""
    z_max = CONFIG["total_h"] if z_max is None else z_max
    v_lo, v_hi = CONFIG["uv_v_min"], CONFIG["uv_v_max"]
    mesh = obj.data
    uv = mesh.uv_layers.get("UVMap") or mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        world_z = (obj.matrix_world @ mesh.vertices[loop.vertex_index].co).z
        t = (world_z - z_min) / (z_max - z_min) if z_max > z_min else 0.5
        t = min(max(t, 0.0), 1.0)
        uv.data[loop.index].uv = (u, v_lo + t * (v_hi - v_lo))
    return obj


def apply_uvs():
    """F.1/F.2 — Gradient-UV ueber die GESAMTE Baugruppe (nicht pro Einzelteil),
    damit der Verlauf durchlaeuft wie bei den nappin-Moebeln.
    Segmente bekommen eine konstante UV, weil sie flaechig leuchten sollen."""
    set_lever(0)                                  # UVs immer im Ruhezustand rechnen
    bpy.context.view_layer.update()

    z_min, z_max = 0.0, CONFIG["total_h"]
    done, flat = [], []
    for obj in _collection().all_objects:
        if obj.type != 'MESH':
            continue
        if obj.name.startswith("Segment_"):
            set_flat_uv(obj, v=0.90)
            flat.append(obj.name)
        else:
            set_gradient_uv(obj, z_min=z_min, z_max=z_max)
            done.append(obj.name)
    return {"gradient": len(done), "flat": len(flat)}


def set_flat_uv(obj, v=0.90, u=0.5):
    """Konstante UV — fuer Segmente, die flaechig leuchten sollen."""
    mesh = obj.data
    uv = mesh.uv_layers.get("UVMap") or mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv.data[loop.index].uv = (u, v)
    return obj


# ----------------------------------------------------------------- BUILD ----

def build_plate():
    """A.1 — Rueckplatte, Wandkontaktebene bei Y = 0."""
    return bevel(make_box("Box_Backplate", **CONFIG["plate"]), width=0.008)


def build_box():
    """A.2/A.5/A.6 — Korpus mit Nische, Tuer (Scharnier-Ursprung), Sichtfenster."""
    C, o, p = CONFIG, CONFIG["box_outer"], CONFIG["pocket"]
    yb, ys, yf = C["box_y_back"], C["box_y_slab"], C["box_y_front"]

    housing = make_multibox("Box_Housing", [
        dict(x0=o["x0"], x1=o["x1"], y0=ys, y1=yb, z0=o["z0"], z1=o["z1"]),   # Rueckslab
        dict(x0=o["x0"], x1=p["x0"], y0=yf, y1=ys, z0=o["z0"], z1=o["z1"]),   # Wand links
        dict(x0=p["x1"], x1=o["x1"], y0=yf, y1=ys, z0=o["z0"], z1=o["z1"]),   # Wand rechts
        dict(x0=p["x0"], x1=p["x1"], y0=yf, y1=ys, z0=o["z0"], z1=p["z0"]),   # Wand unten
        dict(x0=p["x0"], x1=p["x1"], y0=yf, y1=ys, z0=p["z1"], z1=o["z1"]),   # Wand oben
    ], origin=(-0.21, -0.185, 0.74))
    bevel(housing, width=0.010)

    door = make_frame("Box_Door", **C["door"], origin=C["door_hinge"])
    bevel(door, width=0.008)
    glass = make_box("Box_DoorGlass", **C["glass"])
    parent_to(glass, door)
    return housing, door, glass


def build_breaker():
    """B.2/B.3/B.4 — Sicherungsblock, Schaltertafel, Drehlager, Riesenhebel."""
    C = CONFIG
    # Dunkle Auskleidung der Nische: Rueckwand + vier Laibungen. Ohne sie wirkt
    # das Fenster wie ein Loch in eine hell erleuchtete Wand.
    p, yf = C["pocket"], C["box_y_front"]
    t = 0.014
    make_multibox("Breaker_Backboard", [
        C["niche_back"],
        dict(x0=p["x0"], x1=p["x1"], y0=yf, y1=-0.145, z0=p["z0"],     z1=p["z0"] + t),
        dict(x0=p["x0"], x1=p["x1"], y0=yf, y1=-0.145, z0=p["z1"] - t, z1=p["z1"]),
        dict(x0=p["x0"], x1=p["x0"] + t, y0=yf, y1=-0.145, z0=p["z0"] + t, z1=p["z1"] - t),
        dict(x0=p["x1"] - t, x1=p["x1"], y0=yf, y1=-0.145, z0=p["z0"] + t, z1=p["z1"] - t),
    ], origin=(-0.21, -0.23, 0.95))
    bevel(make_box("Breaker_Base",  **C["breaker"]),    width=0.010)
    bevel(make_box("Breaker_Plate", **C["lev_plate"]),  width=0.006)
    bevel(make_box("Breaker_Boss",  **C["lever_boss"]), width=0.012)
    lever = make_multibox("Breaker_Lever",
                          [C["lever_arm"], C["lever_knob"]],
                          origin=C["lever_pivot"])
    bevel(lever, width=0.012)
    return lever


def segment_slots():
    """Z-Grenzen der Segmente, von unten nach oben. Rein rechnerisch —
    seg_count ist ein Parameter, kein fest verdrahteter Wert."""
    C = CONFIG
    n, gap = C["seg_count"], C["seg_gap"]
    span = C["seg_z1"] - C["seg_z0"]
    h = (span - gap * (n - 1)) / n
    return [(C["seg_z0"] + i * (h + gap), C["seg_z0"] + i * (h + gap) + h) for i in range(n)]


def build_column():
    """C.1/C.2 — Saeule als U-Profil: Rueckslab, zwei Schienen, Fuss, Kappe."""
    C = CONFIG
    bevel(make_box("Column_Slab",  **C["col_slab"]),  width=0.008)
    bevel(make_box("Column_RailL", **C["col_railL"]), width=0.006)
    bevel(make_box("Column_RailR", **C["col_railR"]), width=0.006)
    bevel(make_box("Column_Base",  **C["col_base"]),  width=0.012)
    bevel(make_box("Column_Cap",   **C["col_cap"]),   width=0.012)


def build_segments():
    """C.3/C.4/C.5 — Segmentstapel unter einem Empty 'Segments' + Trennstege."""
    C = CONFIG
    slots = segment_slots()

    # Alte Segmente restlos weg (auch wenn seg_count frueher groesser war)
    for i in range(64):
        purge(f"Segment_{i:02d}")
    old = bpy.data.objects.get("Segments")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)

    root = bpy.data.objects.new("Segments", None)   # Empty als Zwischenknoten
    root.empty_display_size = 0.05
    link(root)

    segs = []
    for i, (z0, z1) in enumerate(slots):
        s = make_box(f"Segment_{i:02d}",
                     x0=C["seg_x0"], x1=C["seg_x1"],
                     y0=C["seg_y0"], y1=C["seg_y1"], z0=z0, z1=z1)
        bevel(s, width=0.004, segments=1)
        parent_to(s, root)
        segs.append(s)

    # Trennstege als EIN Objekt — liest die Segmentierung auch aus Distanz
    ribs = []
    for i in range(len(slots) - 1):
        ribs.append(dict(x0=C["seg_x0"] - 0.004, x1=C["seg_x1"] + 0.004,
                         y0=-0.238, y1=C["seg_y1"],
                         z0=slots[i][1], z1=slots[i + 1][0]))
    r = make_multibox("Column_Ribs", ribs, origin=(0.36, -0.226, 0.73))
    return segs, r


def build_details():
    """D.1–D.4 — Kabelrohr, Warnschild, Schrauben, Tuergriff."""
    import math

    # D.1 Kabelrohr Kasten -> Saeule (der visuelle Beweis der Zugehoerigkeit)
    # Tritt aus der Kastenoberseite aus und laeuft in die Saeulenkappe.
    # Y bewusst dicht an der Platte, damit es nicht vor dem Kasten schwebt.
    conduit = make_multibox("Conduit", [
        dict(x0=-0.085, x1=-0.025, y0=-0.155, y1=-0.095, z0=1.14, z1=1.34),   # senkrecht
        dict(x0=-0.085, x1=0.200,  y0=-0.155, y1=-0.095, z0=1.28, z1=1.34),   # waagerecht
    ], origin=(0.0, -0.125, 1.25))
    bevel(conduit, width=0.010)

    # D.2 Warnschild auf dem freien Streifen der Rueckplatte, leicht schief
    sign = make_box("Sign_Warning", x0=-0.46, x1=-0.10, y0=-0.080, y1=-0.050,
                    z0=0.030, z1=0.250, origin=(-0.28, -0.065, 0.140))
    sign.rotation_euler = (0.0, math.radians(-7.0), 0.0)
    bevel(sign, width=0.006)

    # D.3 Ueberdimensionierte Eckschrauben auf der Rueckplatte
    for i, (x, z) in enumerate([(-0.545, 0.045), (0.545, 0.045),
                                (-0.545, 1.405), (0.545, 1.405)]):
        make_cylinder(f"Screw_{i:02d}", radius=0.022, depth=0.03,
                      location=(x, -0.058, z), axis='Y', verts=8)

    # D.4 Tuergriff — an der Tuer, dreht also mit
    door = bpy.data.objects.get("Box_Door")
    handle = make_multibox("Box_Handle", [
        dict(x0=0.020, x1=0.050, y0=-0.395, y1=-0.345, z0=0.855, z1=1.045),
    ], origin=(0.035, -0.370, 0.950))
    bevel(handle, width=0.008)
    if door:
        parent_to(handle, door)
    return conduit, sign, handle


def set_lever(deg):
    """Hebelstellung. 0 = EIN (senkrecht), CONFIG['lever_blowout_deg'] = geflogen.
    Rotation um die Y-Achse -> der Hebel schwenkt FLACH vor der Front,
    kann also nie mit Tuer oder Glas kollidieren."""
    import math
    lever = bpy.data.objects.get("Breaker_Lever")
    if lever:
        lever.rotation_euler = (0.0, math.radians(deg), 0.0)
        bpy.context.view_layer.update()
    return lever


def preview_load(fraction01):
    """Simuliert die Unity-Laufzeitlogik in Blender: gefuellte Segmente bekommen
    ihre Bandfarbe, der Rest das Off-Material. Formel identisch zu SystemLoadBarUI.
    Nur zur Vorschau — vor dem Export assign_materials() aufrufen."""
    n = CONFIG["seg_count"]
    filled = max(0, min(n, round(fraction01 * n)))
    for i in range(n):
        obj = bpy.data.objects.get(f"Segment_{i:02d}")
        if obj:
            assign_mat(obj, SEG_MAT[seg_band(i)] if i < filled else "(Mat)FuseSegment_Off")
    return filled


def set_door(deg):
    """Tuerstellung. 0 = zu. Scharnier ist die LINKE Kante, Drehung um Z.
    Glas und Griff sind an die Tuer geparentet und drehen mit."""
    import math
    door = bpy.data.objects.get("Box_Door")
    if door:
        door.rotation_euler = (0.0, 0.0, math.radians(deg))
        bpy.context.view_layer.update()
    return door


def lever_sweep_bounds():
    """Extrem-Positionen der Hebelspitze ueber den gesamten Schwenk — Kollisionscheck."""
    import math
    lever = bpy.data.objects.get("Breaker_Lever")
    if lever is None:
        return None
    old = tuple(lever.rotation_euler)
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for deg in range(0, int(CONFIG["lever_blowout_deg"]) + 1, 10):
        set_lever(deg)
        for corner in lever.bound_box:
            w = lever.matrix_world @ Vector(corner)
            for i in range(3):
                lo[i] = min(lo[i], w[i]); hi[i] = max(hi[i], w[i])
    lever.rotation_euler = old
    bpy.context.view_layer.update()
    return {"min": [round(v, 3) for v in lo], "max": [round(v, 3) for v in hi]}


# -------------------------------------------------------------- FINALIZE ----

def bake_rotations():
    """G.3 — Objektrotationen ins Mesh backen (nur Sign_Warning hat eine).
    Der Hebel bleibt ausgenommen: seine Rotation ist zur Laufzeit steuerbar."""
    baked = []
    for obj in _collection().all_objects:
        if obj.type != 'MESH' or obj.name == "Breaker_Lever":
            continue
        if any(abs(a) > 1e-6 for a in obj.rotation_euler):
            obj.data.transform(obj.rotation_euler.to_matrix().to_4x4())
            obj.rotation_euler = (0.0, 0.0, 0.0)
            baked.append(obj.name)
    bpy.context.view_layer.update()
    return baked


def build_hierarchy():
    """G.2 — FuseBox_Root als gemeinsamer Elternknoten (Ursprung = Wandebene,
    X-Mitte, Unterkante). Bereits geparentete Objekte behalten ihren Elternteil."""
    old = bpy.data.objects.get("FuseBox_Root")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)

    root = bpy.data.objects.new("FuseBox_Root", None)
    root.empty_display_size = 0.12
    root.location = (0.0, 0.0, 0.0)
    link(root)
    bpy.context.view_layer.update()

    for obj in list(_collection().all_objects):
        if obj is root or obj.parent is not None:
            continue
        parent_to(obj, root)
    return root


def drop_reference():
    """G.1 — Massstabs-Proxys entfernen."""
    col = bpy.data.collections.get("_Ref")
    if col is None:
        return []
    names = [o.name for o in list(col.objects)]
    for o in list(col.objects):
        purge(o.name)
    bpy.data.collections.remove(col)
    return names


def export_fbx(path):
    """G.6 — FBX-Export. Blender-Default -Z Forward / Y Up ergibt in Unity
    korrekt +Z forward / +Y up. Modifier werden beim Export gebacken, damit
    die .blend parametrisch editierbar bleibt."""
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)

    # Bild-Nodes fuer den Export abhaengen: die Gradient-Rampen sind reine
    # Blender-Vorschau. Blieben sie drin, schriebe der Exporter Texturverweise
    # ins FBX, die Unity neben der FBX-Datei sucht und nicht findet.
    # Unity bindet die Materialien ueber den NAMEN — Texturen braucht es dafuer nicht.
    stashed = []
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                stashed.append((node, node.image))
                node.image = None

    bpy.ops.object.select_all(action='DESELECT')
    for obj in _collection().all_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["FuseBox_Root"]

    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        apply_scale_options='FBX_SCALE_ALL',
        apply_unit_scale=True,
        use_mesh_modifiers=True,          # Bevel wird hier gebacken
        mesh_smooth_type='FACE',          # flat shaded — passt zum nappin-Look
        bake_space_transform=False,
        axis_forward='-Z', axis_up='Y',
        use_triangles=False,
        add_leaf_bones=False,
        # STRIP statt AUTO: die Gradient-Rampen haengen nur an der Blender-Vorschau.
        # AUTO wuerde absolute, maschinenspezifische Pfade ins FBX schreiben.
        # Unity bindet die Materialien ohnehin ueber den NAMEN, nicht ueber die Textur.
        path_mode='STRIP',
        embed_textures=False,
    )

    for node, img in stashed:          # Vorschau wiederherstellen
        node.image = img

    return os.path.getsize(path)


# ------------------------------------------------------------------ UTIL ----

def tri_count():
    total = 0
    for obj in _collection().all_objects:
        if obj.type != 'MESH':
            continue
        depsgraph = bpy.context.evaluated_depsgraph_get()
        eval_obj = obj.evaluated_get(depsgraph)
        mesh = eval_obj.to_mesh()
        mesh.calc_loop_triangles()
        total += len(mesh.loop_triangles)
        eval_obj.to_mesh_clear()
    return total


def qa():
    """Abschlusspruefung vor dem Export. Gibt eine Liste von Befunden zurueck —
    leer heisst sauber."""
    issues = []
    col = _collection()
    meshes = [o for o in col.all_objects if o.type == 'MESH']

    for o in meshes:
        if not o.data.materials or o.data.materials[0] is None:
            issues.append(f"{o.name}: kein Material")
        if not o.data.uv_layers:
            issues.append(f"{o.name}: keine UV-Map")
        if any(abs(s - 1.0) > 1e-6 for s in o.scale):
            issues.append(f"{o.name}: Scale != 1 ({tuple(round(s,3) for s in o.scale)})")
        if ".00" in o.name:
            issues.append(f"{o.name}: Duplikat-Suffix im Namen")
        if o.name not in ("Breaker_Lever", "Box_Door") and any(abs(a) > 1e-6 for a in o.rotation_euler):
            issues.append(f"{o.name}: ungebackene Rotation")

    for name in ("Breaker_Lever", "Box_Door"):
        o = bpy.data.objects.get(name)
        if o and any(abs(a) > 1e-6 for a in o.rotation_euler):
            issues.append(f"{name}: steht nicht in Ruhestellung (vor dem Export zuruecksetzen)")

    root = bpy.data.objects.get("FuseBox_Root")
    if root is None:
        issues.append("FuseBox_Root fehlt")
    else:
        for o in col.all_objects:
            if o is root:
                continue
            top = o
            while top.parent:
                top = top.parent
            if top is not root:
                issues.append(f"{o.name}: haengt nicht unter FuseBox_Root")

    n_seg = len([o for o in meshes if o.name.startswith("Segment_")])
    if n_seg != CONFIG["seg_count"]:
        issues.append(f"Segmentzahl {n_seg} != CONFIG {CONFIG['seg_count']}")

    return issues


def purge_orphans():
    """Verwaiste Datenbloecke entfernen (Default-Material, ungenutzte Meshes)."""
    removed = []
    for coll in (bpy.data.materials, bpy.data.meshes, bpy.data.images):
        for db in list(coll):
            if db.users == 0:
                removed.append(db.name)
                coll.remove(db)
    return removed


def report():
    col = _collection()
    names = sorted(o.name for o in col.all_objects)
    return {"count": len(names), "objects": names, "tris": tri_count()}
