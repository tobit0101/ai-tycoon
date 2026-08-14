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
    # Asset-Version: hochzaehlen, wenn sich der Export inhaltlich aendert.
    # 1 = Modellbau (Commit 6bb5fd2), 2 = Textur-Stufe (Atlas-UVs, gebackene Maps,
    # FX-Anker, Decals), 3 = Sicherungsbank in der Nische statt rotem Block.
    "ASSET_VERSION": 3,

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
    # Sicherungsbank in der Nische (seit ASSET_VERSION 3): DIN-Schiene mit
    # din_count Modulen samt Kipphebeln. Modulanzahl = sichtbare Denklast-
    # Kapazitaet (Unity blendet Breaker_Mod_XX/Breaker_Tog_XX paarweise);
    # Breaker_Bank kippt beim Blowout alle Kipphebel synchron zum Riesenhebel.
    # Grosse Massen bleiben neutral-grau — Rot erscheint in der Nische nur im
    # Fehlerfall (Laufzeit-Tint), damit der rote Riesenhebel der Held bleibt.
    "din_center_x": -0.21,
    "din_count": 6,
    "din_pitch": 0.067,
    "din_module": dict(w=0.056, d=0.050, h=0.160),
    "din_module_y1": -0.160,       # Modul-Rueckseite (zur Nischenwand)
    "din_module_z0": 0.860,
    "din_rail":   dict(x0=-0.410, x1=-0.010, y0=-0.185, y1=-0.160, z0=0.925, z1=0.955),
    "din_term":   dict(x0=-0.400, x1=-0.020, y0=-0.195, y1=-0.160, z0=1.040, z1=1.080),
    "din_bus":    dict(count=3, w=0.020, y0=-0.192, y1=-0.160, z0=0.780, z1=0.860),
    "din_rod":    dict(x0=-0.222, x1=-0.198, y0=-0.206, y1=-0.182, z0=0.774, z1=0.930),
    "din_toggle": dict(w=0.030, y0=-0.238, y1=-0.210, z0=0.893, z1=0.950),
    "din_bank_pivot": (-0.21, -0.216, 0.905),
    "bank_blowout_deg": -55.0,
    # Dunkle Nischenrueckwand — sonst verliert der Sicherungsblock im hellen
    # Gehaeuseinneren jeden Kontrast.
    "niche_back": dict(x0=-0.44, x1=0.02, y0=-0.152, y1=-0.140, z0=0.76, z1=1.14),
    # Schaltertafel unterhalb des Fensters, auf der Kastenfront
    "lev_plate": dict(x0=-0.45, x1=0.03, y0=-0.340, y1=-0.320, z0=0.320, z1=0.690),
    # Drehpunkt des Riesenhebels. Rotation um die X-Achse: der Hebel kippt wie
    # ein Messerschalter NACH VORNE-UNTEN aus der Wand — dieselbe Achse und
    # Richtung wie die Bank-Kipphebel in der Nische. (Bis ASSET_VERSION 3 war
    # es ein seitlicher Schwenk um Y; physisch liest sich der Vorwaertskipp
    # richtig, und er kollidiert mit nichts: Tafel und Lagerbock liegen hinter,
    # die Tuer ueber der Schwenkebene.)
    "lever_pivot": (-0.21, -0.360, 0.470),
    "lever_arm":  dict(x0=-0.255, x1=-0.165, y0=-0.405, y1=-0.360, z0=0.470, z1=0.655),
    "lever_knob": dict(x0=-0.295, x1=-0.125, y0=-0.425, y1=-0.350, z0=0.630, z1=0.700),
    "lever_boss": dict(x0=-0.280, x1=-0.140, y0=-0.368, y1=-0.320, z0=0.400, z1=0.540),
    # 135 Grad Vorwaertskipp: deutlich ueber die Horizontale hinaus nach unten,
    # liest sich als "geflogen". Schwenkraum mit lever_sweep_bounds() pruefbar;
    # der Hebel ragt am Scheitel (~90 Grad) rund 0.6 m in den Raum — gewollt,
    # das ist der Comedy-Moment.
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
    "Breaker_Rail":  "(Mat)MetallicBlack",
    "Breaker_Term":  "(Mat)GradientDarkGrey",
    "Breaker_Bus":   "(Mat)GradientYellow",
    "Breaker_Rod":   "(Mat)MetallicBlack",
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

def material_for(name):
    """nappin-Quellmaterial eines Bauteils. Die DIN-Module/-Kipphebel sind
    dynamisch (din_count), deshalb hier statt weiterer ASSIGNMENT-Eintraege.
    Kipphebel neutral dunkel: Rot zeigt Unity nur im Fehlerfall per Tint."""
    if name.startswith("Breaker_Mod_"):
        return "(Mat)GradientGrey"
    if name.startswith("Breaker_Tog_"):
        return "(Mat)MetallicBlack"
    return ASSIGNMENT[name]


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
    """E.3/E.4 — Material je Bauteil, Segmente nach Farbband.

    Seit der Textur-Stufe (ASSET_VERSION 2): Existiert das gebackene M_FuseBox,
    teilen sich alle Atlas-Teile dieses eine Material. Ausnahmen:
      - Box_DoorGlass bleibt (Mat)Glass — Transparenz laesst sich nicht in ein
        opakes Atlas-Material backen.
      - Segmente bleiben (Mat)FuseSegment_Off — LoadPillar faerbt sie zur
        Laufzeit per MaterialPropertyBlock ein.
      - Decal_* bekommen M_FuseBox_Labels (Alpha-Clip aufs Sticker-Sheet).
    Solange M_FuseBox fehlt, faellt alles auf die nappin-Zuordnung zurueck."""
    baked = bpy.data.materials.get("M_FuseBox")
    missing = []
    names = list(ASSIGNMENT) + [f"Breaker_Mod_{i:02d}" for i in range(CONFIG["din_count"])] \
        + [f"Breaker_Tog_{i:02d}" for i in range(CONFIG["din_count"])]
    for obj_name in names:
        obj = bpy.data.objects.get(obj_name)
        if obj is None:
            missing.append(obj_name)
            continue
        if baked and obj_name != "Box_DoorGlass":
            assign_mat(obj, "M_FuseBox")
        else:
            assign_mat(obj, material_for(obj_name))

    if bpy.data.materials.get("M_FuseBox_Labels"):
        for obj in _collection().all_objects:
            if obj.type == 'MESH' and obj.name.startswith("Decal_"):
                spec = LABELS.get(obj.name, {})
                assign_mat(obj, spec.get("mat", "M_FuseBox_Labels"))

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


# ---------------------------------------------------------- TEXTURE BAKE ----
# Textur-Stufe (ASSET_VERSION 2): Cube-Projection-Atlas, zwei gebackene Maps
# (BaseColor sRGB + AO Non-Color), Decal-Quads aufs Sticker-Sheet.
# Der Look der Gradient-Rampen wird dabei nicht ersetzt, sondern IN den Bake
# uebernommen: die nappin-Rampe wird ueber die Welt-Hoehe gesampelt (frueher
# war das ein UV-Trick, die UVs sind jetzt der Atlas).

PROJECT_DIR = "/Users/tobias/develop/ai-tycoon"
ART_TEX_DIR = PROJECT_DIR + "/Assets/_AITycoon/Art/Textures"
PATTERN_PATH = PROJECT_DIR + "/Art_Source/T_FuseBox_Pattern.png"
LABELS_SHEET = ART_TEX_DIR + "/T_FuseBox_Labels.png"

# Dosierung. Zielbild "dezent angebraucht": Materialitaet ja, Muster nein.
PATTERN_SCALE = 1.5      # m pro Kachel — eine Kachel deckt ~das ganze Modell
PATTERN_STRENGTH = 0.30  # Einblendung des Grundmusters (um seinen Mittelwert)
EDGE_STRENGTH = 0.12     # Kantenaufhellung ueber Pointiness
DUST_STRENGTH = 0.16     # Staub, nur auf nach oben zeigenden Flaechen
# Deutlich dunkler als das helle Gehaeuse: bei (0.62, 0.60, 0.55) war der
# Effekt auf hellgrauen Oberseiten messbar unsichtbar (<1%), so sind es ~5%
# warme Abdunklung — lesbar als Staub, immer noch unaufdringlich.
DUST_COLOR = (0.45, 0.43, 0.38)


def bake_objects():
    """Alle Meshes, die in den Textur-Atlas gehen. Ausgenommen:
    - Segment_*: LoadPillar faerbt sie zur Laufzeit per MaterialPropertyBlock —
      eine gebackene Textur wuerde dagegen arbeiten.
    - Decal_*: eigene triviale 0-1-UVs aufs Label-Sheet.
    - Box_DoorGlass: Transparenz passt nicht in ein opakes Atlas-Material."""
    objs = []
    for o in _collection().all_objects:
        if o.type != 'MESH':
            continue
        if o.name.startswith(("Segment_", "Decal_")) or o.name == "Box_DoorGlass":
            continue
        objs.append(o)
    return objs


def unwrap_for_bake(atlas_px=1024, margin_px=16):
    """Atlas-UVs: Cube Projection pro Objekt — das Modell ist zu ~95%
    achsparallele Quader, das ist berechenbarer und verzerrungsaermer als
    Smart UV Project. Gegenueberliegende Boxflaechen projizieren zunaechst
    aufeinander, sind aber getrennte Inseln — pack_islands zieht sie
    auseinander, danach ueberlappt nichts mehr.
    average_islands_scale sorgt vorher fuer gleiche Texeldichte ueberall."""
    set_lever(0)
    set_door(0)
    objs = bake_objects()
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.context.scene.tool_settings.use_uv_select_sync = True

    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=1.0, correct_aspect=True,
                            scale_to_bounds=False)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(margin=margin_px / atlas_px, rotate=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    return {"objects": len(objs), "uv_margin": round(margin_px / atlas_px, 4)}


def texel_density_report(resolutions=(512, 1024, 2048)):
    """UV-Flaeche gegen Weltflaeche -> px/m je Kandidaten-Aufloesung.
    Sinnvolles Band fuer ein Prop aus Iso-Distanz: 256-512 px/m."""
    import math
    uv_area = world_area = 0.0
    for o in bake_objects():
        mesh = o.data
        uv = mesh.uv_layers.active.data
        for poly in mesh.polygons:
            world_area += poly.area              # Scale ist 1 -> Weltflaeche
            pts = [uv[li].uv for li in poly.loop_indices]
            a = 0.0
            for i in range(len(pts)):
                x0, y0 = pts[i]
                x1, y1 = pts[(i + 1) % len(pts)]
                a += x0 * y1 - x1 * y0
            uv_area += abs(a) * 0.5
    density = math.sqrt(uv_area / world_area) if world_area else 0.0
    out = {"uv_area": round(uv_area, 4),
           "world_area_m2": round(world_area, 3),
           "uv_utilisation": round(uv_area * 100, 1)}
    for r in resolutions:
        out[f"px_per_m@{r}"] = round(density * r)
    return out


def _pattern_image():
    img = bpy.data.images.get("T_FuseBox_Pattern.png")
    if img is None:
        img = bpy.data.images.load(PATTERN_PATH)
    # Non-Color: das Muster MODULIERT nur Helligkeit, es ist keine Farbe
    img.colorspace_settings.name = 'Non-Color'
    return img


def _pattern_mean(img):
    """Mittelwert des Musters. Die Einblendung rechnet (Wert - Mittel) *
    Staerke + 1 — dadurch ist sie unabhaengig davon, wie hell das generierte
    Bild ausgefallen ist."""
    import numpy as np
    arr = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(arr)
    return float(arr.reshape(-1, 4)[:, :3].mean())


def _bake_source_materials():
    """Namen der nappin-Quellmaterialien, die im Atlas aufgehen."""
    return sorted({material_for(o.name) for o in bake_objects()})


def build_bake_materials():
    """Prozedurale Bake-Quellmaterialien — enthalten den heutigen Look statt
    ihn zu ersetzen: nappin-Rampe (ueber Welt-Hoehe gesampelt) x Grundmuster
    (Box-Projektion, haengt NICHT am Unwrap) x Kantenaufhellung (Pointiness)
    x Staub (nur auf Flaechen mit Normale nach oben)."""
    C = CONFIG
    pattern = _pattern_image()
    mean = _pattern_mean(pattern)
    made = []

    for src_name in _bake_source_materials():
        spec = MATERIALS[src_name]
        mat_name = "M_Bake_" + src_name
        mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()

        out = nt.nodes.new("ShaderNodeOutputMaterial")
        out.location = (1300, 0)
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.location = (1050, 0)
        # Metallic bewusst 0: bei Metallen liefe der Diffuse-Color-Bake leer
        bsdf.inputs["Roughness"].default_value = spec.get("roughness", 0.6)
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

        geo = nt.nodes.new("ShaderNodeNewGeometry")
        geo.location = (-1400, 0)

        # Grundfarbe: Rampe ueber Welt-Z (identisch zum bisherigen UV-Remap)
        if spec.get("tex"):
            sep = nt.nodes.new("ShaderNodeSeparateXYZ")
            sep.location = (-1200, 250)
            nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
            rng = nt.nodes.new("ShaderNodeMapRange")
            rng.location = (-1000, 250)
            rng.inputs["From Min"].default_value = 0.0
            rng.inputs["From Max"].default_value = C["total_h"]
            rng.inputs["To Min"].default_value = C["uv_v_min"]
            rng.inputs["To Max"].default_value = C["uv_v_max"]
            nt.links.new(sep.outputs["Z"], rng.inputs["Value"])
            comb = nt.nodes.new("ShaderNodeCombineXYZ")
            comb.location = (-800, 250)
            comb.inputs["X"].default_value = 0.5
            nt.links.new(rng.outputs["Result"], comb.inputs["Y"])
            ramp = nt.nodes.new("ShaderNodeTexImage")
            ramp.location = (-600, 250)
            ramp.extension = 'EXTEND'
            tex_path = NAPPIN_TEX.format(spec["tex"])
            img = bpy.data.images.get(tex_path.split("/")[-1])
            ramp.image = img or bpy.data.images.load(tex_path)
            nt.links.new(comb.outputs["Vector"], ramp.inputs["Vector"])
            base_socket = ramp.outputs["Color"]
        else:
            rgb = nt.nodes.new("ShaderNodeRGB")
            rgb.location = (-600, 250)
            rgb.outputs[0].default_value = (*spec["base"], 1.0)
            base_socket = rgb.outputs[0]

        # Grundmuster: Box-Projektion in Weltkoordinaten, 1 Kachel ~ Modell
        pscale = nt.nodes.new("ShaderNodeVectorMath")
        pscale.operation = 'SCALE'
        pscale.location = (-1200, -150)
        nt.links.new(geo.outputs["Position"], pscale.inputs[0])
        pscale.inputs["Scale"].default_value = 1.0 / PATTERN_SCALE
        ptex = nt.nodes.new("ShaderNodeTexImage")
        ptex.location = (-1000, -150)
        ptex.image = pattern
        ptex.projection = 'BOX'
        ptex.projection_blend = 0.3
        nt.links.new(pscale.outputs["Vector"], ptex.inputs["Vector"])
        psub = nt.nodes.new("ShaderNodeMath")
        psub.operation = 'SUBTRACT'
        psub.location = (-800, -150)
        nt.links.new(ptex.outputs["Color"], psub.inputs[0])
        psub.inputs[1].default_value = mean
        pmul = nt.nodes.new("ShaderNodeMath")
        pmul.operation = 'MULTIPLY'
        pmul.location = (-650, -150)
        nt.links.new(psub.outputs[0], pmul.inputs[0])
        pmul.inputs[1].default_value = PATTERN_STRENGTH
        padd = nt.nodes.new("ShaderNodeMath")
        padd.operation = 'ADD'
        padd.location = (-500, -150)
        nt.links.new(pmul.outputs[0], padd.inputs[0])
        padd.inputs[1].default_value = 1.0
        patterned = nt.nodes.new("ShaderNodeVectorMath")
        patterned.operation = 'SCALE'
        patterned.location = (-300, 100)
        nt.links.new(base_socket, patterned.inputs[0])
        nt.links.new(padd.outputs[0], patterned.inputs["Scale"])

        # Kantenaufhellung: Pointiness -> schmale Rampe -> MULTIPLIKATIV
        # (Farbe x (1 + Kante x Staerke)). Additiver Weissmix wuerde dunkle
        # Teile (MetallicBlack-Schrauben, Griff) relativ um ein Vielfaches
        # aufhellen — multiplikativ skaliert der Effekt mit der Grundhelligkeit.
        emap = nt.nodes.new("ShaderNodeMapRange")
        emap.location = (-300, -350)
        emap.inputs["From Min"].default_value = 0.50
        emap.inputs["From Max"].default_value = 0.62
        nt.links.new(geo.outputs["Pointiness"], emap.inputs["Value"])
        emul = nt.nodes.new("ShaderNodeMath")
        emul.operation = 'MULTIPLY'
        emul.location = (-100, -350)
        nt.links.new(emap.outputs["Result"], emul.inputs[0])
        emul.inputs[1].default_value = EDGE_STRENGTH
        eadd = nt.nodes.new("ShaderNodeMath")
        eadd.operation = 'ADD'
        eadd.location = (0, -350)
        nt.links.new(emul.outputs[0], eadd.inputs[0])
        eadd.inputs[1].default_value = 1.0
        emix = nt.nodes.new("ShaderNodeVectorMath")
        emix.operation = 'SCALE'
        emix.location = (100, 100)
        nt.links.new(patterned.outputs["Vector"], emix.inputs[0])
        nt.links.new(eadd.outputs[0], emix.inputs["Scale"])

        # Staub: physikalisch ehrlich nur dort, wo die Normale nach oben zeigt
        nsep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nsep.location = (-300, -550)
        nt.links.new(geo.outputs["Normal"], nsep.inputs["Vector"])
        dmap = nt.nodes.new("ShaderNodeMapRange")
        dmap.location = (-100, -550)
        dmap.inputs["From Min"].default_value = 0.55
        dmap.inputs["From Max"].default_value = 0.95
        nt.links.new(nsep.outputs["Z"], dmap.inputs["Value"])
        dmul = nt.nodes.new("ShaderNodeMath")
        dmul.operation = 'MULTIPLY'
        dmul.location = (100, -550)
        nt.links.new(dmap.outputs["Result"], dmul.inputs[0])
        dmul.inputs[1].default_value = DUST_STRENGTH
        dmix = nt.nodes.new("ShaderNodeMix")
        dmix.data_type = 'RGBA'
        dmix.location = (350, 100)
        nt.links.new(dmul.outputs[0], dmix.inputs["Factor"])
        nt.links.new(emix.outputs["Vector"], dmix.inputs["A"])
        dmix.inputs["B"].default_value = (*DUST_COLOR, 1.0)

        nt.links.new(dmix.outputs["Result"], bsdf.inputs["Base Color"])

        # Bake-Ziel-Node: bake_maps setzt das Image und macht ihn AKTIV.
        # Bewusst unverbunden — er darf den Shader nicht beeinflussen.
        tgt = nt.nodes.new("ShaderNodeTexImage")
        tgt.name = "BakeTarget"
        tgt.label = "BakeTarget"
        tgt.location = (1050, -350)
        made.append(mat_name)
    return made


def bake_maps(resolution=1024, margin_px=16, ao_samples=64, device='CPU'):
    """AO- und BaseColor-Bake in den gemeinsamen Atlas (Cycles zwingend).

    Die vier klassischen Stolperstellen sind hier abgeraeumt:
    - Engine explizit CYCLES (EEVEE kann nicht backen),
    - alle Atlas-Objekte gleichzeitig selektiert, in jedem Material zeigt der
      AKTIVE Node aufs Ziel-Image (nicht etwa der Grundmuster-Node),
    - AO beruecksichtigt die gesamte Baugruppe: Segmente und Glas bleiben
      sichtbar und WERFEN Verschattung, empfangen aber nichts (nicht selektiert),
    - Farbraeume: BaseColor sRGB, AO Non-Color."""
    import os
    scene = bpy.context.scene
    set_lever(0)
    set_door(0)

    # Immer frisch bauen: unbenutzte Materialien ueberleben ein Speichern der
    # .blend nicht (0 Nutzer -> Purge) — auf den Dateizustand ist kein Verlass.
    build_bake_materials()

    for o in bake_objects():
        assign_mat(o, "M_Bake_" + material_for(o.name))

    # Decals sitzen 1-2 mm VOR den Flaechen und wuerfen sonst AO-Schatten
    hidden = []
    for o in _collection().all_objects:
        if o.type == 'MESH' and o.name.startswith("Decal_") and not o.hide_render:
            o.hide_render = True
            hidden.append(o)

    def _image(name, srgb):
        img = bpy.data.images.get(name)
        if img and tuple(img.size) != (resolution, resolution):
            bpy.data.images.remove(img)
            img = None
        if img is None:
            img = bpy.data.images.new(name, resolution, resolution, alpha=False)
        img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
        return img

    img_base = _image("T_FuseBox_BaseColor", srgb=True)
    img_ao = _image("T_FuseBox_AO", srgb=False)

    mats = [bpy.data.materials["M_Bake_" + n] for n in _bake_source_materials()]

    def _target(img):
        for m in mats:
            node = m.node_tree.nodes["BakeTarget"]
            node.image = img
            for n in m.node_tree.nodes:
                n.select = False
            node.select = True
            m.node_tree.nodes.active = node

    scene.render.engine = 'CYCLES'
    # Default ist CPU: der Metal-GPU-Bake hat Blender 5.2 reproduzierbar zum
    # Absturz gebracht. Den Bake deshalb am besten headless in einem separaten
    # Prozess fahren (siehe README), dann reisst ein Crash keine Session mit.
    if device == 'GPU':
        try:
            prefs = bpy.context.preferences.addons["cycles"].preferences
            prefs.compute_device_type = 'METAL'
            for d in prefs.devices:
                d.use = True
            scene.cycles.device = 'GPU'
        except Exception as exc:
            print("GPU nicht verfuegbar, backe auf CPU:", exc)
            scene.cycles.device = 'CPU'
    else:
        scene.cycles.device = 'CPU'

    bake = scene.render.bake
    bake.margin = margin_px
    bake.margin_type = 'EXTEND'       # Dilation — gegen Insel-Bluten im Mip-Mapping
    bake.use_selected_to_active = False
    bake.use_clear = True

    bpy.ops.object.select_all(action='DESELECT')
    objs = bake_objects()
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]

    scene.cycles.samples = ao_samples
    _target(img_ao)
    bpy.ops.object.bake(type='AO')

    scene.cycles.samples = 32
    _target(img_base)
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'})

    for o in hidden:
        o.hide_render = False

    os.makedirs(ART_TEX_DIR, exist_ok=True)
    paths = {}
    for img, fname in ((img_base, "T_FuseBox_BaseColor.png"),
                       (img_ao, "T_FuseBox_AO.png")):
        img.filepath_raw = ART_TEX_DIR + "/" + fname
        img.file_format = 'PNG'
        img.save()
        paths[img.name] = img.filepath_raw
    return paths


def build_atlas_material():
    """M_FuseBox — das finale Material mit den gebackenen Maps (folgt der
    Eigen-Asset-Konvention M_LoadSegment). Der AO-Multiply hier dient NUR der
    Blender-Vorschau; in Unity liegt AO als eigene Map im Occlusion-Slot,
    damit die Staerke gegen die SSAO regelbar bleibt."""
    mat = bpy.data.materials.get("M_FuseBox") or bpy.data.materials.new("M_FuseBox")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (350, 0)
    bsdf.inputs["Roughness"].default_value = 0.6
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    def _map(name, srgb):
        img = bpy.data.images.get(name)
        if img is None:
            img = bpy.data.images.load(ART_TEX_DIR + "/" + name + ".png")
            img.name = name
        img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
        return img

    base = nt.nodes.new("ShaderNodeTexImage")
    base.location = (-300, 150)
    base.image = _map("T_FuseBox_BaseColor", srgb=True)
    ao = nt.nodes.new("ShaderNodeTexImage")
    ao.location = (-300, -200)
    ao.image = _map("T_FuseBox_AO", srgb=False)

    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    mix.location = (50, 100)
    mix.inputs["Factor"].default_value = 1.0
    nt.links.new(base.outputs["Color"], mix.inputs["A"])
    nt.links.new(ao.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    return mat


# ----------------------------------------------------------------- LABELS ----

# Decal-Quads: uv = (u0, v0, u1, v1) im Sticker-Sheet (v von unten),
# size = (Breite, Hoehe) in m, center = Weltpunkt 2 mm vor der Zielflaeche.
# rot_y wird ins MESH gebacken (Sign_Warning ist um -7 Grad gedreht, das
# Decal muss mitdrehen; Objektrotation bleibt 0 -> qa() bleibt sauber).
# Die UV-Rechtecke sind aus dem generierten Sheet GEMESSEN (Magenta-Keying,
# Bounding-Box je Element, 4 px Rand), nicht geschaetzt. Quad-Seitenverhaeltnis
# = Seitenverhaeltnis des UV-Ausschnitts, sonst verzerrt der Sticker.
LABELS = {
    # Warndreieck mittig auf dem Warnschild — dreht die -7 Grad des Schilds mit.
    # Achtung Sheet-Layout: Dreieck (bis x=403) und Kreis (ab x=385) ueberlappen
    # sich im X-Band; der linke Kreis-Sliver ist deshalb im PNG geloescht, damit
    # dieses Fenster die volle Dreieckskontur fassen kann, ohne ein Fragment
    # des Nachbarn einzufangen.
    "Decal_Sign": dict(center=(-0.28, -0.0825, 0.140), size=(0.171, 0.160),
                       uv=(0.0360, 0.5280, 0.3955, 0.8640), rot_y=-7.0),
    # Ampere-Typenschild links auf der Schaltertafel (ausserhalb des Hebel-Schwenks)
    "Decal_Plate": dict(center=(-0.39, -0.342, 0.60), size=(0.090, 0.090),
                        uv=(0.6611, 0.5498, 0.9639, 0.8525)),
    # STROMLAST-Label auf der Saeulenkappe
    "Decal_Column": dict(center=(0.36, -0.297, 1.37), size=(0.289, 0.100),
                         uv=(0.0342, 0.2734, 0.6631, 0.4912)),
    # Warnstreifen auf dem Saeulenfuss
    "Decal_ColumnBase": dict(center=(0.36, -0.297, 0.08), size=(0.340, 0.070),
                             uv=(0.0371, 0.1172, 0.6602, 0.2451)),
    # kleiner Hinweisaufkleber (Stecker) auf der Kastenfront rechts der Tuer
    "Decal_Housing": dict(center=(0.0775, -0.322, 0.50), size=(0.038, 0.049),
                          uv=(0.6895, 0.1328, 0.9541, 0.4756)),
    # Schaltnetz-Grafik auf der Nischenrueckwand (hinter dem Sichtfenster).
    # 4 mm vor der Rueckwand; die Schnittlinie mit dem Sicherungsblock liegt
    # unsichtbar in dessen Volumen. Eigene opake Textur, eigenes Material.
    # UV beschneidet den strukturlosen dunklen Bildrand, damit auch aus
    # schraegen Blickwinkeln Leitungen neben dem Sicherungsblock sichtbar sind.
    "Decal_Niche": dict(center=(-0.21, -0.156, 0.95), size=(0.42, 0.34),
                        uv=(0.04, 0.04, 0.96, 0.96), mat="M_FuseBox_Circuit"),
}

CIRCUIT_TEX = ART_TEX_DIR + "/T_FuseBox_Circuit.png"


def _make_decal(name, center, size, uv_rect, rot_y_deg=0.0):
    """Quad in der XZ-Ebene, Front nach -Y, triviale UVs auf den Sheet-Ausschnitt."""
    import math
    purge(name)
    w, h = size
    u0, v0, u1, v1 = uv_rect
    verts = [(-w / 2, 0, -h / 2), (w / 2, 0, -h / 2),
             (w / 2, 0, h / 2), (-w / 2, 0, h / 2)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], [(0, 1, 2, 3)])   # Normale zeigt nach -Y
    mesh.update()
    uv = mesh.uv_layers.new(name="UVMap")
    for li, coord in zip(mesh.polygons[0].loop_indices,
                         [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]):
        uv.data[li].uv = coord
    if abs(rot_y_deg) > 1e-6:
        mesh.transform(Matrix.Rotation(math.radians(rot_y_deg), 4, 'Y'))
    obj = bpy.data.objects.new(name, mesh)
    obj.location = center
    link(obj)
    shade_flat(obj)
    return obj


def build_labels_material():
    mat = (bpy.data.materials.get("M_FuseBox_Labels")
           or bpy.data.materials.new("M_FuseBox_Labels"))
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (350, 0)
    bsdf.inputs["Roughness"].default_value = 0.55
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.location = (-200, 0)
    img = bpy.data.images.get("T_FuseBox_Labels.png")
    tex.image = img or bpy.data.images.load(LABELS_SHEET)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    # hartes Alpha-Clipping (Sticker-Kante), robust in EEVEE wie Cycles
    clip = nt.nodes.new("ShaderNodeMath")
    clip.operation = 'GREATER_THAN'
    clip.location = (50, -200)
    clip.inputs[1].default_value = 0.5
    nt.links.new(tex.outputs["Alpha"], clip.inputs[0])
    nt.links.new(clip.outputs[0], bsdf.inputs["Alpha"])
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = 'DITHERED'
    return mat


def build_circuit_material():
    """M_FuseBox_Circuit — opake Schaltnetz-Grafik fuer die Nischenrueckwand."""
    mat = (bpy.data.materials.get("M_FuseBox_Circuit")
           or bpy.data.materials.new("M_FuseBox_Circuit"))
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (350, 0)
    bsdf.inputs["Roughness"].default_value = 0.6
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.location = (-200, 0)
    img = bpy.data.images.get("T_FuseBox_Circuit.png")
    tex.image = img or bpy.data.images.load(CIRCUIT_TEX)
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def build_labels():
    """Decal-Quads 1-2 mm vor der jeweiligen Flaeche — voellig unabhaengig
    vom Atlas-Unwrap. Alle statisch, daher direkt unter FuseBox_Root."""
    build_labels_material()
    build_circuit_material()
    root = bpy.data.objects.get("FuseBox_Root")
    made = []
    for name, spec in LABELS.items():
        obj = _make_decal(name, spec["center"], spec["size"], spec["uv"],
                          spec.get("rot_y", 0.0))
        assign_mat(obj, spec.get("mat", "M_FuseBox_Labels"))
        if root:
            parent_to(obj, root)
        made.append(name)
    return made


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


def din_slots():
    """X-Grenzen der DIN-Module, links nach rechts — Raster aus CONFIG gerechnet,
    analog segment_slots(): din_count ist Parameter, kein fest verdrahteter Wert."""
    C = CONFIG
    n, pitch, w = C["din_count"], C["din_pitch"], C["din_module"]["w"]
    span = (n - 1) * pitch + w
    x0 = C["din_center_x"] - span / 2
    return [(x0 + i * pitch, x0 + i * pitch + w) for i in range(n)]


def build_bank():
    """Sicherungsbank in der Nische: DIN-Schiene, Klemmenstreifen, Sammelschienen,
    Schubstange (die sichtbare Kopplung zum Riesenhebel) und din_count Module
    mit Kipphebeln. Die Kipphebel haengen unter dem Empty Breaker_Bank
    (Ursprung = gemeinsame Kippachse) und schwenken beim Blowout synchron.
    Bewusst OHNE Bevel: bei 2-6-cm-Bauteilen unsichtbar, kostet nur Tris."""
    C = CONFIG

    # Altbestand restlos weg (auch wenn din_count frueher groesser war)
    for i in range(32):
        purge(f"Breaker_Mod_{i:02d}")
        purge(f"Breaker_Tog_{i:02d}")
    old = bpy.data.objects.get("Breaker_Bank")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)

    make_box("Breaker_Rail", **C["din_rail"])
    make_box("Breaker_Term", **C["din_term"])

    slots = din_slots()
    bus = C["din_bus"]
    centers = [(s[0] + s[1]) / 2 for s in slots]
    picks = [centers[0], centers[len(centers) // 2], centers[-1]][:bus["count"]]
    make_multibox("Breaker_Bus",
                  [dict(x0=x - bus["w"] / 2, x1=x + bus["w"] / 2,
                        y0=bus["y0"], y1=bus["y1"], z0=bus["z0"], z1=bus["z1"])
                   for x in picks],
                  origin=(C["din_center_x"], bus["y0"], (bus["z0"] + bus["z1"]) / 2))
    make_box("Breaker_Rod", **C["din_rod"])

    bank = bpy.data.objects.new("Breaker_Bank", None)
    bank.empty_display_size = 0.03
    bank.location = C["din_bank_pivot"]
    link(bank)

    m, t = C["din_module"], C["din_toggle"]
    y1, z0 = C["din_module_y1"], C["din_module_z0"]
    for i, (x0, x1) in enumerate(slots):
        make_box(f"Breaker_Mod_{i:02d}", x0=x0, x1=x1,
                 y0=y1 - m["d"], y1=y1, z0=z0, z1=z0 + m["h"])
        cx = (x0 + x1) / 2
        tog = make_box(f"Breaker_Tog_{i:02d}",
                       x0=cx - t["w"] / 2, x1=cx + t["w"] / 2,
                       y0=t["y0"], y1=t["y1"], z0=t["z0"], z1=t["z1"])
        parent_to(tog, bank)

    root = bpy.data.objects.get("FuseBox_Root")
    if root:
        parent_to(bank, root)
    return bank


def set_bank(deg):
    """Kipphebel-Bank. 0 = EIN, CONFIG['bank_blowout_deg'] = geflogen.
    Rotation um die X-Achse (Kippachse der Schiene) — in Unity lokal X."""
    import math
    bank = bpy.data.objects.get("Breaker_Bank")
    if bank:
        bank.rotation_euler = (math.radians(deg), 0.0, 0.0)
        bpy.context.view_layer.update()
    return bank


def build_breaker():
    """B.2/B.3/B.4 — Nischen-Sicherungsbank, Schaltertafel, Drehlager, Riesenhebel."""
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
    purge("Breaker_Base")            # roter Platzhalter-Block aus ASSET_VERSION <= 2
    build_bank()
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


def build_fx_anchors():
    """FX-Ansatzpunkte als Empties unter FuseBox_Root — exportieren mit ins FBX.
    Spart einen kompletten Re-Export, wenn spaeter Partikeleffekte dazukommen.
    Positionen sind aus CONFIG abgeleitet, nicht hart eingetippt.
    FX_LeverTip markiert die Hebelspitze in RUHESTELLUNG (der Hebel selbst
    rotiert zur Laufzeit; wer die bewegte Spitze braucht, parented in Unity um)."""
    C = CONFIG
    rail, cap, knob = C["din_rail"], C["col_cap"], C["lever_knob"]
    anchors = {
        # Funken: mittig knapp vor der Kipphebel-Reihe der Sicherungsbank
        "FX_Breaker": ((rail["x0"] + rail["x1"]) / 2,
                       C["din_toggle"]["y0"] - 0.02,
                       (rail["z0"] + rail["z1"]) / 2),
        # Rauch/Statuslicht: mittig ueber der Saeulenkappe
        "FX_ColumnTop": ((cap["x0"] + cap["x1"]) / 2,
                         (cap["y0"] + cap["y1"]) / 2, cap["z1"] + 0.03),
        # Hebelende (Knauf-Mitte, Ruhestellung)
        "FX_LeverTip": ((knob["x0"] + knob["x1"]) / 2, knob["y0"],
                        (knob["z0"] + knob["z1"]) / 2),
    }
    root = bpy.data.objects.get("FuseBox_Root")
    made = []
    for name, loc in anchors.items():
        purge(name)
        e = bpy.data.objects.new(name, None)
        e.empty_display_type = 'PLAIN_AXES'
        e.empty_display_size = 0.04
        e.location = loc
        link(e)
        if root:
            parent_to(e, root)
        made.append(name)
    return made


def set_lever(deg):
    """Hebelstellung. 0 = EIN (senkrecht), CONFIG['lever_blowout_deg'] = geflogen.
    Rotation um die X-Achse, negativ angewandt -> der Hebel kippt wie ein
    Messerschalter nach VORNE-UNTEN aus der Wand (gleiche Bewegungsrichtung
    wie die Bank-Kipphebel, siehe set_bank)."""
    import math
    lever = bpy.data.objects.get("Breaker_Lever")
    if lever:
        lever.rotation_euler = (math.radians(-deg), 0.0, 0.0)
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

    for name in ("Breaker_Lever", "Box_Door", "Breaker_Bank"):
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
