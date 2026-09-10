using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Furnishes the ground floor — the lobby a visitor lands in — as a gallery about the
    /// culture the eighteen games above come out of. Eight freestanding plaques
    /// (<see cref="LobbyLessonContent"/>) and four things to set off: the gong ageng by the
    /// entrance (<see cref="GongStation"/>), the gasing sculpture in the atrium
    /// (<see cref="GasingStation"/>), a walk-on engklek court (<see cref="HopscotchCourse"/>) and a
    /// tembang stage (<see cref="SongStation"/>) in the west hall, plus a table of the games' tools
    /// in the east hall.
    /// </summary>
    /// <remarks>
    /// Menu: <c>Museum/Decor/Build Lobby Gallery</c> (and <c>Clear</c>). Idempotent: one
    /// scene-root container, <c>Lobby Gallery (Generated)</c>, rebuilt from scratch, and the
    /// eight <c>lobby_*</c> lesson assets rewritten from their editor-side source.
    ///
    /// Placement is authored here in lobby coordinates (floor 1.60 m; interior x 3–50,
    /// z −26–13) against the building as it stands: the reception desk, the stairs, the gasing
    /// pedestal and the gong are all part of the merged building meshes and are measured, not
    /// moved. Every prop's footprint is checked against
    /// <see cref="MuseumRoomDecorBuilder.Obstacles"/> and reported, never silently overlapped.
    ///
    /// The plaques reuse <see cref="MuseumLessonUIBuilder.BuildPanelAt"/>, so the lobby reads
    /// like the storeys above; each stands in a teak frame on two posts because the lobby's
    /// walls are glass and pilaster, with nowhere to hang.
    /// </remarks>
    public static class MuseumLobbyGalleryBuilder
    {
        public const string RootName = "Lobby Gallery (Generated)";

        private const float Floor = 1.60f;

        /// <summary>Same reading height as the storeys' plaques (MuseumLessonUIBuilder).</summary>
        private const float ReadingHeight = 1.88f;

        private const float PlaqueWidth = 2.6f;
        private const float WelcomeWidth = 3.0f;

        // Carpentry stock, shared with the signage.
        private const float BoardDepth = 0.05f;
        private const float LipThickness = 0.02f;
        private const float LipDepth = 0.03f;
        private const float CanvasLift = 0.008f;
        private const float FrameMargin = 0.12f;
        private const float PostSize = 0.10f;

        /// <summary>Head height of the "Tekan Enter" hints and the court's readout.</summary>
        private const float HintHeight = 2.55f;

        [MenuItem("Museum/Decor/Build Lobby Gallery")]
        public static void Build()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            Material wood = MuseumDecorMaterials.FrameWood();
            Material gold = MuseumDecorMaterials.FrameGold();
            Material teak = MuseumDecorMaterials.Teak();
            if (wood == null || gold == null || teak == null) return;

            MuseumScreenGeometry.ClearRoot(scene, RootName);
            Transform root = MuseumScreenGeometry.EnsureRoot(scene, RootName);

            var lessons = new Dictionary<string, GameLessonData>();
            foreach (LobbyLesson source in LobbyLessonContent.All()) lessons[source.Key] = WriteLesson(source);

            var ctx = new Context
            {
                Root = root,
                Wood = wood,
                Gold = gold,
                Teak = teak,
                Obstacles = MuseumRoomDecorBuilder.Obstacles(),
                Lessons = lessons,
            };

            BuildWelcome(ctx);
            BuildGong(ctx);
            BuildGasing(ctx);
            BuildEngklek(ctx);
            BuildTembang(ctx);
            BuildRagam(ctx);
            BuildFilosofi(ctx);
            BuildEtika(ctx);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MuseumLobbyGalleryBuilder: built 8 stations under '{RootName}' " +
                      $"({ctx.Collisions} footprint warning(s)).");
        }

        [MenuItem("Museum/Decor/Clear Lobby Gallery")]
        public static void Clear()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            if (!MuseumScreenGeometry.ClearRoot(scene, RootName))
            {
                Debug.Log("MuseumLobbyGalleryBuilder: nothing to clear.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private class Context
        {
            public Transform Root;
            public Material Wood, Gold, Teak;
            public List<Bounds> Obstacles;
            public Dictionary<string, GameLessonData> Lessons;
            public int Collisions;
        }

        // --- stations ------------------------------------------------------------------

        /// <summary>
        /// The welcome plaque stands behind the reception desk facing the doors, flanked by two
        /// ceremonial payung, so it is the first thing read on the way in. Its reader trigger
        /// is the strip between the desk and the doors.
        /// </summary>
        private static void BuildWelcome(Context ctx)
        {
            Transform station = Station(ctx, "Sambutan");
            var centre = new Vector3(26.4f, Floor, 5.3f);

            Plaque(ctx, station, "Sambutan", ctx.Lessons["lobby_sambutan"], centre, Vector3.back, WelcomeWidth,
                   "SELAMAT DATANG");

            Prop(ctx, station, "Payung_Ceremonial", centre + new Vector3(-2.3f, 0f, 0f));
            Prop(ctx, station, "Payung_Ceremonial", centre + new Vector3(2.3f, 0f, 0f));

            GameObject volume = Volume(station, "Reader Volume", new Vector3(26.4f, 0f, 9.8f), new Vector3(6f, 2.2f, 3.6f));
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        /// <summary>
        /// The gong ageng is one of the building's merged meshes, so the striking side is measured
        /// off its vertices: the disc's normal is the mesh's up axis, the face toward the doors is
        /// the +normal side. A tabuh hangs from a bracket on the beam in front of that face and
        /// swings in about the axis across it.
        /// </summary>
        private static void BuildGong(Context ctx)
        {
            GameObject gong = GameObject.Find("Gong_Ageng_Lobby");
            if (gong == null)
            {
                Debug.LogWarning("MuseumLobbyGalleryBuilder: 'Gong_Ageng_Lobby' not found; gong station skipped.");
                return;
            }

            Transform station = Station(ctx, "Gong");
            Vector3 normal = gong.transform.up;
            if (normal.z < 0f) normal = -normal;
            Vector3 disc = DiscCentre(gong, normal);
            Vector3 swingAxis = Vector3.Cross(Vector3.up, normal);

            // Bracket and tabuh in the gong's own frame: local +Z is the striking normal.
            var rig = new GameObject("Tabuh Rig");
            Undo.RegisterCreatedObjectUndo(rig, UndoLabel);
            rig.transform.SetParent(station, false);
            rig.transform.SetPositionAndRotation(disc, Quaternion.LookRotation(normal));

            Material iron = MuseumDecorMaterials.Iron();
            Material cloth = MuseumDecorMaterials.ClothRed();

            // The stand's beam runs 0.8–1.2 m above disc centre; the bracket rises off its face
            // and reaches out over the striking spot.
            MuseumScreenFrameBuilder.Bar(rig.transform, "Bracket Post", iron, new Vector3(0f, 1.2f, 0.09f), new Vector3(0.05f, 0.35f, 0.05f));
            MuseumScreenFrameBuilder.Bar(rig.transform, "Bracket Arm", iron, new Vector3(0f, 1.35f, 0.35f), new Vector3(0.05f, 0.05f, 0.62f));

            var hinge = new GameObject("Tabuh");
            Undo.RegisterCreatedObjectUndo(hinge, UndoLabel);
            hinge.transform.SetParent(rig.transform, false);
            // 1.3 m arm × sin 22° ≈ 0.49 m of travel: the head starts 0.51 m off the disc and lands on it.
            hinge.transform.localPosition = new Vector3(0f, 1.32f, 0.6f);

            const float arm = 1.3f;
            MuseumScreenFrameBuilder.Bar(hinge.transform, "Shaft", ctx.Teak, new Vector3(0f, -arm * 0.5f, 0f), new Vector3(0.035f, arm, 0.035f));
            MuseumScreenFrameBuilder.Bar(hinge.transform, "Head", cloth, new Vector3(0f, -arm, 0f), new Vector3(0.18f, 0.2f, 0.18f));
            Movable(hinge);

            GameObject volume = Volume(station, "Station Volume", disc + normal * 1.9f + new Vector3(0.6f, 0f, 0f), new Vector3(5f, 2.2f, 3f));
            AudioSource source = Audio(volume);
            Vector3 hintAt = disc + normal * 1.3f;
            TMP_Text hint = Hint(station, new Vector3(hintAt.x, Floor + HintHeight, hintAt.z), "Tekan Enter — tabuh gong");

            var gongStation = Undo.AddComponent<GongStation>(volume);
            var so = new SerializedObject(gongStation);
            Set(so, "mallet", hinge.transform);
            so.FindProperty("swingAxis").vector3Value = swingAxis;
            Set(so, "source", source);
            Set(so, "hint", hint);
            so.FindProperty("hintText").stringValue = "Tekan Enter — tabuh gong";
            so.ApplyModifiedPropertiesWithoutUndo();

            // The plaque stands east of the gong, turned to the striking spot.
            var plaqueAt = new Vector3(39.6f, Floor, 7.2f);
            Plaque(ctx, station, "Gong", ctx.Lessons["lobby_gong"], plaqueAt, Vector3.right, PlaqueWidth, "GALERI LANTAI DASAR");
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        /// <summary>
        /// The atrium's gasing sculpture: body, shoulder and stem are separate scene meshes on a
        /// pedestal, spun about the pedestal's centre line. The trigger is the whole rug, so the
        /// plaque on its north edge and the Enter both work wherever the visitor stands.
        /// </summary>
        private static void BuildGasing(Context ctx)
        {
            Transform station = Station(ctx, "Gasing");
            var parts = new List<Transform>();
            foreach (string name in new[] { "Gasing_body", "Gasing_shoulder", "Gasing_stem" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null) parts.Add(go.transform);
                else Debug.LogWarning($"MuseumLobbyGalleryBuilder: '{name}' not found; the gasing will spin without it.");
            }

            GameObject pedestal = GameObject.Find("Gasing_pedestal");
            Vector3 axis = pedestal != null
                ? new Vector3(pedestal.GetComponent<Renderer>().bounds.center.x, Floor, pedestal.GetComponent<Renderer>().bounds.center.z)
                : new Vector3(29.44f, Floor, -6.83f);

            GameObject volume = Volume(station, "Station Volume", new Vector3(axis.x, 0f, -7.5f), new Vector3(8f, 2.2f, 9.6f));
            AudioSource source = Audio(volume);
            TMP_Text hint = Hint(station, new Vector3(axis.x, Floor + HintHeight, -9.5f), "Tekan Enter — putar gasing");

            var gasing = Undo.AddComponent<GasingStation>(volume);
            var so = new SerializedObject(gasing);
            FillArray(so.FindProperty("parts"), parts.ToArray());
            so.FindProperty("axisPoint").vector3Value = axis;
            Set(so, "source", source);
            Set(so, "hint", hint);
            so.FindProperty("hintText").stringValue = "Tekan Enter — putar gasing";
            so.ApplyModifiedPropertiesWithoutUndo();

            Plaque(ctx, station, "Gasing", ctx.Lessons["lobby_gasing"], new Vector3(axis.x, 0f, -12.6f), Vector3.back, PlaqueWidth, "GALERI LANTAI DASAR");
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        /// <summary>
        /// A chalk engklek court on the west hall's floor: seven numbered petak and the gunung,
        /// climbing north from petak 1, each a trigger the visitor's capsule steps into.
        /// </summary>
        private static void BuildEngklek(Context ctx)
        {
            Transform station = Station(ctx, "Engklek");
            const float x0 = 10f, z0 = -3.5f, pitch = 0.95f, half = 0.475f;

            // (x offset, row) per petak, bottom to top; the gunung is the last.
            var layout = new (float dx, int row)[]
            {
                (0f, 0), (0f, 1), (-half, 2), (half, 2), (0f, 3), (-half, 4), (half, 4),
            };

            var court = new GameObject("Court");
            Undo.RegisterCreatedObjectUndo(court, UndoLabel);
            court.transform.SetParent(station, false);

            Material chalk = MuseumDecorMaterials.ChalkWhite();
            Material face = PetakMaterial();

            var tiles = new List<HopscotchTile>();
            var rows = new List<int>();
            for (int i = 0; i < layout.Length; i++)
            {
                var at = new Vector3(x0 + layout[i].dx, Floor, z0 - layout[i].row * pitch);
                tiles.Add(Petak(court.transform, $"Petak {i + 1}", at, new Vector2(0.9f, 0.9f), chalk, face, (i + 1).ToString()));
                rows.Add(layout[i].row);
            }

            // Gunung: the half-disc at the top, wide enough to stand on with both feet.
            var top = new Vector3(x0, Floor, z0 - 5f * pitch - 0.15f);
            tiles.Add(Petak(court.transform, "Gunung", top, new Vector2(1.85f, 0.55f), chalk, face, "GUNUNG", round: true));
            rows.Add(5);

            var volume = new GameObject("Course");
            Undo.RegisterCreatedObjectUndo(volume, UndoLabel);
            volume.transform.SetParent(station, false);
            volume.transform.position = new Vector3(x0, Floor + 1.1f, z0 - 2.5f * pitch);
            AudioSource source = Audio(volume);

            // Readout above the plaque's frame, so the two never overlap.
            float plaqueZ = top.z - 1.35f;
            TMP_Text status = Label(station, "Status", new Vector3(x0, PlaqueTop(PlaqueWidth) + 0.22f, plaqueZ), Vector3.back, 3.2f, 8f, Gold);

            var course = Undo.AddComponent<HopscotchCourse>(volume);
            var so = new SerializedObject(course);
            FillArray(so.FindProperty("tiles"), tiles.ToArray());
            SerializedProperty rowsProp = so.FindProperty("rows");
            rowsProp.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++) rowsProp.GetArrayElementAtIndex(i).intValue = rows[i];
            Set(so, "source", source);
            Set(so, "status", status);
            so.ApplyModifiedPropertiesWithoutUndo();

            // The plaque past the gunung, read from the top of the court; its trigger is the court.
            Plaque(ctx, station, "Engklek", ctx.Lessons["lobby_engklek"], new Vector3(x0, Floor, plaqueZ), Vector3.back, PlaqueWidth, "GALERI LANTAI DASAR");
            GameObject reader = Volume(station, "Reader Volume", new Vector3(x0, 0f, z0 - 2.6f * pitch), new Vector3(4f, 2.2f, 7.4f));
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), reader);

            // A gacuk to lend the court: a stack of roof-tile shards beside petak 1.
            Prop(ctx, station, "Cirak_Tiles", new Vector3(x0 + 1.4f, Floor, z0 + 0.2f), 0.8f);
        }

        /// <summary>
        /// A low teak stage with a tikar, a small gong stand and a cushion — the dolanan songs'
        /// corner. The lyric banner hangs above the plaque behind it.
        /// </summary>
        private static void BuildTembang(Context ctx)
        {
            Transform station = Station(ctx, "Tembang");
            var centre = new Vector3(10f, 0f, -18f);
            const float stageW = 4.2f, stageD = 2.6f, stageH = 0.15f;

            var stage = new GameObject("Panggung");
            Undo.RegisterCreatedObjectUndo(stage, UndoLabel);
            stage.transform.SetParent(station, false);
            stage.transform.position = new Vector3(centre.x, Floor, centre.z);
            MuseumScreenFrameBuilder.Bar(stage.transform, "Deck", ctx.Teak, new Vector3(0f, stageH * 0.5f, 0f), new Vector3(stageW, stageH, stageD));
            MuseumScreenFrameBuilder.Bar(stage.transform, "Skirt", ctx.Wood, new Vector3(0f, stageH * 0.5f, 0f), new Vector3(stageW + 0.06f, stageH - 0.03f, stageD + 0.06f));
            var deck = stage.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, stageH * 0.5f, 0f);
            deck.size = new Vector3(stageW, stageH, stageD);
            Check(ctx, "Panggung", new Bounds(new Vector3(centre.x, Floor + stageH * 0.5f, centre.z), new Vector3(stageW, stageH, stageD)));

            float deckTop = Floor + stageH;
            Prop(ctx, station, "Tikar_Mat", new Vector3(centre.x, deckTop, centre.z + 0.1f), 1f, 0f, check: false);
            Prop(ctx, station, "Gong_Stand", new Vector3(centre.x - 1.45f, deckTop, centre.z - 0.55f), 1f, 25f, check: false);
            Prop(ctx, station, "Suweng_Cushion", new Vector3(centre.x + 0.9f, deckTop, centre.z + 0.35f), 1f, -15f, check: false);
            Prop(ctx, station, "Kendhi_Pot", new Vector3(centre.x + 1.6f, deckTop, centre.z - 0.7f), 1f, 0f, check: false);
            Prop(ctx, station, "Bathok_Shells", new Vector3(centre.x - 0.2f, deckTop, centre.z - 0.6f), 1f, 40f, check: false);

            GameObject volume = Volume(station, "Station Volume", new Vector3(centre.x, 0f, centre.z + stageD * 0.5f + 1.5f), new Vector3(5.2f, 2.2f, 3f));
            AudioSource source = Audio(volume);
            TMP_Text hint = Hint(station, new Vector3(centre.x, Floor + HintHeight, centre.z + stageD * 0.5f + 0.2f), "Tekan Enter — dengar tembang");

            Vector3 plaqueAt = new Vector3(centre.x, 0f, centre.z - stageD * 0.5f - 0.55f);
            Plaque(ctx, station, "Tembang", ctx.Lessons["lobby_tembang"], plaqueAt, Vector3.back, PlaqueWidth, "GALERI LANTAI DASAR");

            // Lyric banner: a teak plate above the plaque's frame.
            Transform banner = Holder("Banner", station, new Vector3(plaqueAt.x, Floor + ReadingHeight + 1.25f, plaqueAt.z), Quaternion.LookRotation(Vector3.back));
            Carpentry(banner, ctx.Wood, ctx.Gold, 3.4f, 0.5f);
            TMP_Text lyric = Label(banner, "Lyric", banner.position - banner.forward * (CanvasLift + LipDepth), Vector3.back, 3.2f, 11f, Gold);

            var song = Undo.AddComponent<SongStation>(volume);
            var so = new SerializedObject(song);
            Set(so, "source", source);
            Set(so, "lyric", lyric);
            Set(so, "hint", hint);
            so.FindProperty("hintText").stringValue = "Tekan Enter — dengar tembang";
            so.ApplyModifiedPropertiesWithoutUndo();

            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        /// <summary>The games' tools laid out on plinth and tables in the east hall, in front of the Ragam plaque.</summary>
        private static void BuildRagam(Context ctx)
        {
            Transform station = Station(ctx, "Ragam");
            var centre = new Vector3(43f, 0f, -8.2f);

            Plaque(ctx, station, "Ragam", ctx.Lessons["lobby_ragam"], new Vector3(centre.x, 0f, centre.z - 1.5f), Vector3.back, PlaqueWidth, "GALERI LANTAI DASAR");

            GameObject plinth = Prop(ctx, station, "Plinth_Teak", new Vector3(centre.x, Floor, centre.z));
            if (plinth != null) Prop(ctx, station, "Dakon_Board", new Vector3(centre.x, Floor + 0.91f, centre.z), 0.65f, 0f, check: false);

            GameObject left = Prop(ctx, station, "Low_Table", new Vector3(centre.x - 1.9f, Floor, centre.z), 1f, 90f);
            if (left != null)
            {
                Prop(ctx, station, "Bekelan_Set", new Vector3(centre.x - 1.9f, Floor + 0.45f, centre.z + 0.3f), 0.9f, 20f, check: false);
                Prop(ctx, station, "Gatheng_Stones", new Vector3(centre.x - 1.9f, Floor + 0.45f, centre.z - 0.3f), 0.9f, 0f, check: false);
            }

            GameObject right = Prop(ctx, station, "Low_Table", new Vector3(centre.x + 1.9f, Floor, centre.z), 1f, 90f);
            if (right != null)
            {
                Prop(ctx, station, "Cirak_Tiles", new Vector3(centre.x + 1.9f, Floor + 0.45f, centre.z + 0.3f), 0.8f, -30f, check: false);
                Prop(ctx, station, "Benthik_Sticks", new Vector3(centre.x + 1.9f, Floor + 0.45f, centre.z - 0.3f), 0.8f, 10f, check: false);
            }

            Prop(ctx, station, "Egrang_Stilts", new Vector3(centre.x + 3.6f, Floor, centre.z - 0.6f), 1f, -20f);
            Prop(ctx, station, "Bamboo_Rack", new Vector3(centre.x - 3.6f, Floor, centre.z - 0.6f), 1f, 0f);
            Prop(ctx, station, "Bitingan_Bundle", new Vector3(centre.x - 3.6f, Floor + 0.02f, centre.z + 0.35f), 1f, 60f, check: false);
            Prop(ctx, station, "Lompat_Tali", new Vector3(centre.x + 3.6f, Floor + 0.02f, centre.z + 0.4f), 1f, 30f, check: false);

            GameObject volume = Volume(station, "Reader Volume", new Vector3(centre.x, 0f, centre.z + 2.2f), new Vector3(8f, 2.2f, 3.2f));
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        /// <summary>The values plaque at the east hall's far end, a kendhi on a plinth each side.</summary>
        private static void BuildFilosofi(Context ctx)
        {
            Transform station = Station(ctx, "Filosofi");
            var at = new Vector3(43f, Floor, -19.5f);
            Plaque(ctx, station, "Filosofi", ctx.Lessons["lobby_filosofi"], at, Vector3.back, PlaqueWidth, "GALERI LANTAI DASAR");

            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 p = at + new Vector3(side * 2.1f, 0f, 0.2f);
                GameObject plinth = Prop(ctx, station, "Plinth_Teak", new Vector3(p.x, Floor, p.z));
                if (plinth != null) Prop(ctx, station, side < 0f ? "Kendhi_Pot" : "Gunungan_Wayang", new Vector3(p.x, Floor + 0.91f, p.z), side < 0f ? 1f : 0.8f, 0f, check: false);
            }

            GameObject volume = Volume(station, "Reader Volume", at + new Vector3(0f, 0f, 2.2f), new Vector3(5.5f, 2.2f, 3.2f));
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        /// <summary>The rules plaque near the east hall's south end, turned to the atrium.</summary>
        private static void BuildEtika(Context ctx)
        {
            Transform station = Station(ctx, "Etika");
            var at = new Vector3(46.2f, Floor, 3.5f);
            Plaque(ctx, station, "Etika", ctx.Lessons["lobby_etika"], at, Vector3.right, PlaqueWidth, "GALERI LANTAI DASAR");

            Prop(ctx, station, "Bamboo_Rack", at + new Vector3(0f, 0f, 2.2f), 1f, -90f);
            Prop(ctx, station, "Blarak_Frond", at + new Vector3(-0.4f, 0f, 2.2f), 1f, 0f, check: false);
            GameObject plinth = Prop(ctx, station, "Plinth_Teak", at + new Vector3(0f, 0f, -2.1f));
            if (plinth != null) Prop(ctx, station, "Ancak_Tray", at + new Vector3(0f, 0.91f, -2.1f), 0.8f, 0f, check: false);

            GameObject volume = Volume(station, "Reader Volume", at + new Vector3(-2.2f, 0f, 0f), new Vector3(3.2f, 2.2f, 5.5f));
            MuseumLessonUIBuilder.AttachReaderTo(station.GetComponentInChildren<LessonPanel>(), volume);
        }

        // --- pieces --------------------------------------------------------------------

        private static Transform Station(Context ctx, string name)
        {
            var go = new GameObject($"Station_{name}");
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(ctx.Root, false);
            return go.transform;
        }

        /// <summary>
        /// A lesson plaque in a freestanding teak frame: board and gold lip round the panel, two
        /// posts down to a foot on the floor. <paramref name="forward"/> is the direction from
        /// the reader into the board.
        /// </summary>
        private static void Plaque(Context ctx, Transform station, string name, GameLessonData lesson, Vector3 floorPoint,
                                   Vector3 forward, float width, string eyebrow)
        {
            Vector2 panel = MuseumLessonUIBuilder.PanelSize;
            float scale = width / panel.x;
            float height = panel.y * scale;
            float frameW = width + FrameMargin;
            float frameH = height + FrameMargin;

            Quaternion rotation = Quaternion.LookRotation(forward);
            var centre = new Vector3(floorPoint.x, Floor + ReadingHeight, floorPoint.z);
            Transform holder = Holder($"Plaque_{name}", station, centre, rotation);
            Carpentry(holder, ctx.Wood, ctx.Gold, frameW, frameH);

            float postLen = ReadingHeight - frameH * 0.5f;
            float postX = frameW * 0.5f - PostSize * 0.6f;
            float postY = -frameH * 0.5f - postLen * 0.5f;
            MuseumScreenFrameBuilder.Bar(holder, "Post Left", ctx.Teak, new Vector3(-postX, postY, BoardDepth * 0.5f), new Vector3(PostSize, postLen, PostSize));
            MuseumScreenFrameBuilder.Bar(holder, "Post Right", ctx.Teak, new Vector3(postX, postY, BoardDepth * 0.5f), new Vector3(PostSize, postLen, PostSize));
            MuseumScreenFrameBuilder.Bar(holder, "Foot", ctx.Wood, new Vector3(0f, -ReadingHeight + 0.04f, 0f), new Vector3(frameW + 0.3f, 0.08f, 0.6f));

            var box = holder.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, (-ReadingHeight + frameH * 0.5f) * 0.5f, 0.1f);
            box.size = new Vector3(frameW + 0.3f, ReadingHeight + frameH * 0.5f, 0.6f);

            MuseumLessonUIBuilder.BuildPanelAt(holder, lesson, $"Lesson Panel ({name})",
                                              centre - forward.normalized * CanvasLift, rotation, scale,
                                              eyebrow, "lobby gallery");

            Vector3 right = rotation * Vector3.right;
            var footprint = new Bounds(new Vector3(centre.x, Floor + (ReadingHeight + frameH * 0.5f) * 0.5f, centre.z), Vector3.zero);
            footprint.Encapsulate(centre + right * (frameW * 0.5f + 0.15f) + forward * 0.3f + Vector3.up * (frameH * 0.5f));
            footprint.Encapsulate(centre - right * (frameW * 0.5f + 0.15f) - forward * 0.3f - Vector3.up * ReadingHeight);
            Check(ctx, $"Plaque_{name}", footprint);
        }

        /// <summary>Height of a plaque frame's top lip, for what hangs above it.</summary>
        private static float PlaqueTop(float width) =>
            Floor + ReadingHeight + (MuseumLessonUIBuilder.PanelSize.y * width / MuseumLessonUIBuilder.PanelSize.x + FrameMargin) * 0.5f;

        private static Transform Holder(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.transform;
        }

        /// <summary>A teak board with a gold lip round its face; local +Z is behind the face.</summary>
        private static void Carpentry(Transform holder, Material wood, Material gold, float width, float height)
        {
            MuseumScreenFrameBuilder.Bar(holder, "Board", wood, new Vector3(0f, 0f, BoardDepth * 0.5f), new Vector3(width, height, BoardDepth));
            MuseumScreenFrameBuilder.Ring(holder, "Lip", gold, width, height, LipThickness, LipDepth, -LipDepth * 0.5f + 0.002f);
        }

        /// <summary>One petak: a chalk outline on the floor, a cream face that lights, its number, and the trigger that reports the step.</summary>
        private static HopscotchTile Petak(Transform court, string name, Vector3 floorPoint, Vector2 size, Material chalk, Material face,
                                           string label, bool round = false)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(court, false);
            go.transform.position = floorPoint;

            Renderer faceRenderer;
            if (round)
            {
                // A flattened cylinder reads as the gunung's arch; the chalk ring is a slightly larger one under it.
                Primitive(go.transform, "Chalk", PrimitiveType.Cylinder, chalk, new Vector3(0f, 0.008f, 0f), new Vector3(size.x + 0.08f, 0.008f, size.y * 2f + 0.08f));
                faceRenderer = Primitive(go.transform, "Face", PrimitiveType.Cylinder, face, new Vector3(0f, 0.02f, 0f), new Vector3(size.x, 0.008f, size.y * 2f));
            }
            else
            {
                MuseumScreenFrameBuilder.Bar(go.transform, "Chalk", chalk, new Vector3(0f, 0.008f, 0f), new Vector3(size.x + 0.08f, 0.016f, size.y + 0.08f));
                faceRenderer = Bar(go.transform, "Face", face, new Vector3(0f, 0.02f, 0f), new Vector3(size.x, 0.016f, size.y));
            }

            // Lit at runtime through an instanced material, so it must not be folded into a static batch.
            GameObjectUtility.SetStaticEditorFlags(faceRenderer.gameObject, 0);

            TMP_Text number = Label(go.transform, "Number", floorPoint + new Vector3(0f, 0.035f, 0f), Vector3.back, size.x * 0.9f,
                                    round ? 24f : 44f, MuseumDecorMaterials.Hex("3A2414"));
            number.text = label;
            // Flat on the floor, read by a visitor walking north up the court.
            number.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.back);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(size.x, 1f, round ? size.y * 2f : size.y);

            var tile = go.AddComponent<HopscotchTile>();
            var so = new SerializedObject(tile);
            Set(so, "face", faceRenderer);
            so.ApplyModifiedPropertiesWithoutUndo();
            return tile;
        }

        /// <summary>Board_Cream with emission switched on, so the lit variant of Lit ships in the build.</summary>
        private static Material PetakMaterial()
        {
            Material m = MuseumDecorMaterials.Get("Petak_Cream", MuseumDecorMaterials.Hex("EFE4CC"), 0f, 0.2f);
            if (m == null) return null;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Renderer Bar(Transform parent, string name, Material material, Vector3 localPos, Vector3 size)
        {
            MuseumScreenFrameBuilder.Bar(parent, name, material, localPos, size);
            return parent.Find(name).GetComponent<Renderer>();
        }

        private static Renderer Primitive(Transform parent, string name, PrimitiveType type, Material material, Vector3 localPos, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            return renderer;
        }

        /// <summary>A generated prop stood on its origin at <paramref name="floorPoint"/>; null if the model is missing or it would overlap the building.</summary>
        private static GameObject Prop(Context ctx, Transform station, string model, Vector3 floorPoint, float scale = 1f, float yaw = 0f, bool check = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{MuseumRoomDecorBuilder.ModelFolder}{model}.fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"MuseumLobbyGalleryBuilder: model '{model}' missing under {MuseumRoomDecorBuilder.ModelFolder}.");
                return null;
            }

            GameObject go = MuseumRoomDecorBuilder.Spawn(prefab, model, station, Quaternion.Euler(0f, yaw, 0f), scale);
            go.transform.position = floorPoint;
            Bounds b = MuseumRoomDecorBuilder.WorldBounds(go);
            if (check) Check(ctx, model, b);
            MuseumRoomDecorBuilder.Finish(go, b);
            return go;
        }

        private static void Check(Context ctx, string what, Bounds b)
        {
            if (MuseumRoomDecorBuilder.Clear(b, ctx.Obstacles)) return;
            ctx.Collisions++;
            Debug.LogWarning($"MuseumLobbyGalleryBuilder: '{what}' at {b.center} overlaps the building ({b.min} .. {b.max}).");
        }

        /// <summary>A trigger box standing on the floor.</summary>
        private static GameObject Volume(Transform station, string name, Vector3 floorCentre, Vector3 size)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(station, false);
            go.transform.position = new Vector3(floorCentre.x, Floor + size.y * 0.5f, floorCentre.z);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return go;
        }

        private static AudioSource Audio(GameObject volume)
        {
            var source = Undo.AddComponent<AudioSource>(volume);
            GalleryStation.TuneSource(source);
            return source;
        }

        /// <summary>The floating "Tekan Enter" over a station; GalleryStation turns it to the camera and shows it only while inside.</summary>
        private static TMP_Text Hint(Transform station, Vector3 position, string text)
        {
            TMP_Text label = Label(station, "Hint", position, Vector3.back, 2.4f, 9f, Cream);
            label.text = text;
            label.fontStyle = FontStyles.Bold;
            return label;
        }

        /// <summary>
        /// A 3D TextMeshPro label (no canvas) of <paramref name="width"/> metres, built at a tenth
        /// scale: a font size of 10 is a line ~0.1 m tall. <paramref name="forward"/> is the
        /// direction from the reader into the text.
        /// </summary>
        private static TMP_Text Label(Transform parent, string name, Vector3 position, Vector3 forward, float width, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            go.transform.localScale = Vector3.one * 0.1f;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width * 10f, 6f);

            var text = go.GetComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldFont);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = "";
            return text;
        }

        /// <summary>Strip the static flags off something that moves at runtime.</summary>
        private static void Movable(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            }
        }

        /// <summary>Centre of the gong's disc from its vertices: those a disc's radius from the origin at disc height, averaged along the normal.</summary>
        private static Vector3 DiscCentre(GameObject gong, Vector3 normal)
        {
            var mf = gong.GetComponent<MeshFilter>();
            Vector3 origin = gong.transform.position;
            if (mf == null || mf.sharedMesh == null) return new Vector3(origin.x, 2.9f, origin.z);

            Matrix4x4 m = gong.transform.localToWorldMatrix;
            double along = 0; double y = 0; int n = 0;
            foreach (Vector3 v in mf.sharedMesh.vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                Vector3 flat = new Vector3(p.x - origin.x, 0f, p.z - origin.z);
                if (p.y < 2.2f || p.y > 3.6f || flat.magnitude > 0.7f) continue;
                along += Vector3.Dot(p, normal);
                y += p.y;
                n++;
            }

            if (n == 0) return new Vector3(origin.x, 2.9f, origin.z);
            float planeAlong = (float)(along / n);
            Vector3 centre = new Vector3(origin.x, (float)(y / n), origin.z);
            return centre + normal * (planeAlong - Vector3.Dot(centre, normal));
        }

        // --- content -------------------------------------------------------------------

        private static GameLessonData WriteLesson(LobbyLesson source)
        {
            string folder = MuseumLessonUIBuilder.LessonFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string path = $"{folder}/{source.Key}.asset";
            var lesson = AssetDatabase.LoadAssetAtPath<GameLessonData>(path);
            if (lesson == null)
            {
                lesson = ScriptableObject.CreateInstance<GameLessonData>();
                AssetDatabase.CreateAsset(lesson, path);
            }

            lesson.gameKey = source.Key;
            lesson.displayName = source.Title;
            lesson.sections = new LessonSection[source.Sections.Length];
            for (int i = 0; i < source.Sections.Length; i++)
            {
                lesson.sections[i] = new LessonSection { heading = source.Sections[i].heading, body = source.Sections[i].body };
            }

            EditorUtility.SetDirty(lesson);
            return lesson;
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogWarning($"MuseumLobbyGalleryBuilder: no serialized field '{field}' on {so.targetObject.GetType().Name}.");
            else p.objectReferenceValue = value;
        }
    }
}
