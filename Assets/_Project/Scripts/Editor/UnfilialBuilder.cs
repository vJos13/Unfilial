using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Genera todo el contenido del juego: arte pixel-art procedural (hojas de sprites y tileset),
/// animaciones + Animator, paleta de tiles, audio (.wav + MainMixer), VFX, prefabs,
/// menú principal y los 3 niveles a partir de los mapas de texto en _Project/Levels.
/// Leyenda: '#' suelo, '^' pinchos, 'P' jugador, 'E' Crawler, 'B' Brute, 'S' Vigía, 'K' jefe, 'o' orbe, 'X' salida.
/// ATENCIÓN: re-ejecutarlo sobrescribe las escenas y assets generados.
/// </summary>
public static class UnfilialBuilder
{
    private const string Root = "Assets/_Project";
    private const string SpriteDir = Root + "/Art/Sprites";
    private const string CharacterDir = SpriteDir + "/Characters"; // Hojas externas (PNG + JSON)
    private const string TileDir = Root + "/Art/Tiles";
    private const string PaletteDir = Root + "/Art/Palettes";
    private const string FontDir = Root + "/Art/Fonts";
    private const string AnimationDir = Root + "/Animations/Player";
    private const string MusicDir = Root + "/Audio/Music";
    private const string SfxDir = Root + "/Audio/SFX";
    private const string MixerPath = Root + "/Audio/MainMixer.mixer";
    private const string VfxDir = Root + "/VFX";
    private const string VfxPrefabDir = VfxDir + "/Prefabs";
    private const string PrefabDir = Root + "/Prefabs";
    private const string SceneDir = Root + "/Scenes";
    private const string LevelDir = Root + "/Levels";
    private const string MainMenuScene = "MainMenu";
    private const int TileSize = 32; // PPU = tamaño del tile

    private static readonly Color Gold = new Color(1f, 0.85f, 0.45f);
    private static readonly Color CrawlerTint = new Color(1f, 0.5f, 0.35f);
    private static readonly Color BruteTint = new Color(0.78f, 0.4f, 0.95f);
    private static readonly Color ShooterTint = new Color(0.45f, 0.9f, 0.8f);
    private static readonly Color OrbTint = new Color(1f, 0.86f, 0.45f);

    private const string ControlsHint =
        "A/D: Mover   ·   Espacio: Saltar (doble)   ·   Shift: Dash   ·   J / Clic izq.: Atacar (+W/S: arriba/abajo)   ·   K / Clic der.: Escudo   ·   Esc: Pausa";

    private class LevelDef
    {
        public string Scene, Number, Name;
        public Color Ground, Background, Sky;
        public int Boss; // índice en Bosses

        public LevelDef(string scene, string number, string name, Color ground, Color background, Color sky, int boss)
        {
            Scene = scene; Number = number; Name = name; Ground = ground; Background = background; Sky = sky; Boss = boss;
        }
    }

    private static readonly LevelDef[] Levels =
    {
        new LevelDef("Level_01", "NIVEL I", "Ruinas Olvidadas",
            new Color(0.70f, 0.78f, 0.92f), new Color(0.42f, 0.50f, 0.68f), new Color(0.06f, 0.08f, 0.13f), 0),
        new LevelDef("Level_02", "NIVEL II", "Cavernas de Ceniza",
            new Color(0.95f, 0.70f, 0.55f), new Color(0.55f, 0.34f, 0.27f), new Color(0.10f, 0.05f, 0.04f), 1),
        new LevelDef("Level_03", "NIVEL III", "Templo Celestial",
            new Color(0.92f, 0.88f, 1.00f), new Color(0.50f, 0.46f, 0.78f), new Color(0.09f, 0.07f, 0.16f), 2),
    };

    private class BossDef
    {
        public string Sheet, Id, Name;
        public BossController.SpecialAttack Special;
        public bool Flying;
        public Vector2 ColliderSize, ColliderOffset, FirePoint, StrikeBox;
        public int Health, Orbs, ProjectileDamage;
        public float Speed, MeleeRange, LungeSpeed, SpawnHeight, ProjectileSpeed, ProjectileScale;
        public Color ProjectileColor;
    }

    // Medidas tomadas de la silueta real de cada hoja (128x128 px, 32 PPU, pivote centrado)
    private static readonly BossDef[] Bosses =
    {
        new BossDef
        {
            Sheet = "boss_guardian", Id = "Guardian", Name = "Guardián de las Ruinas", Special = BossController.SpecialAttack.GroundWave,
            ColliderSize = new Vector2(1.3f, 3.1f), ColliderOffset = new Vector2(0f, -0.35f), FirePoint = new Vector2(-1.2f, -1.4f),
            StrikeBox = new Vector2(2.6f, 3f), Health = 24, Orbs = 15, Speed = 2.2f, MeleeRange = 3.2f, LungeSpeed = 10f, SpawnHeight = 1.93f,
            ProjectileColor = new Color(0.5f, 0.95f, 1f), ProjectileSpeed = 8f, ProjectileScale = 1.6f, ProjectileDamage = 20,
        },
        new BossDef
        {
            Sheet = "boss_ceniza", Id = "Ceniza", Name = "Bestia de Ceniza", Special = BossController.SpecialAttack.LavaSpit,
            ColliderSize = new Vector2(2.9f, 2.3f), ColliderOffset = new Vector2(0.1f, -0.75f), FirePoint = new Vector2(-1.6f, -0.6f),
            StrikeBox = new Vector2(3f, 2.2f), Health = 36, Orbs = 20, Speed = 3f, MeleeRange = 3.6f, LungeSpeed = 13f, SpawnHeight = 1.93f,
            ProjectileColor = new Color(1f, 0.5f, 0.2f), ProjectileSpeed = 9f, ProjectileScale = 1.1f, ProjectileDamage = 15,
        },
        new BossDef
        {
            Sheet = "boss_heraldo", Id = "Heraldo", Name = "Heraldo Celestial", Special = BossController.SpecialAttack.LightLances, Flying = true,
            ColliderSize = new Vector2(1.4f, 2.8f), ColliderOffset = new Vector2(0.1f, 0.1f), FirePoint = new Vector2(-1.2f, 0.6f),
            StrikeBox = new Vector2(2.8f, 3f), Health = 48, Orbs = 25, Speed = 3.2f, MeleeRange = 3.5f, LungeSpeed = 12f, SpawnHeight = 4f,
            ProjectileColor = new Color(1f, 0.9f, 0.55f), ProjectileSpeed = 11f, ProjectileScale = 0.9f, ProjectileDamage = 15,
        },
    };

    private class Art
    {
        public Sheet PlayerSheet;
        public Sheet[] BossSheets;
        public RuntimeAnimatorController[] BossControllers;
        public Sprite Orb, Crawler, Brute, Shooter, Slash, Shield, Gate, UIWhite;
        public TileBase[] GroundByMask; // índice = arriba(8) | abajo(4) | izquierda(2) | derecha(1) expuestos
        public TileBase GroundCracked, Spikes, BackWall, BackWallAlt;
        public PhysicsMaterial2D NoFriction;
        public RuntimeAnimatorController PlayerController;
    }

    private class Prefabs
    {
        public GameObject Player, Crawler, Brute, Shooter, Projectile, Orb, Exit, Canvas, GameManager, AudioManager, HitVfx;
        public GameObject[] Bosses;
    }

    private static int GroundLayer => LayerMask.NameToLayer("Ground");
    private static int PlayerLayer => LayerMask.NameToLayer("Player");
    private static int EnemyLayer => LayerMask.NameToLayer("Enemy");
    private static int UILayer => LayerMask.NameToLayer("UI");

    [MenuItem("Unfilial/Construir Juego Completo (Menú + 3 Niveles)")]
    public static void BuildAll()
    {
        if (GroundLayer < 0 || PlayerLayer < 0 || EnemyLayer < 0)
        {
            Debug.LogError("Faltan las layers 'Ground', 'Player' o 'Enemy' en Tags & Layers.");
            return;
        }

        // Guardado automático (sin diálogos modales) de la escena abierta antes de reemplazarla
        EditorSceneManager.SaveOpenScenes();

        // Jugador y enemigos se atraviesan (el daño por contacto se detecta por solapamiento)
        Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, true);

        foreach (string dir in new[] { SpriteDir, TileDir, PaletteDir, FontDir, AnimationDir, MusicDir, SfxDir, VfxPrefabDir, PrefabDir, SceneDir })
        {
            Directory.CreateDirectory(dir);
        }
        AssetDatabase.Refresh();

        Art art = BuildArt();
        BuildPalette(art);
        AudioAssets audio = BuildAudio();
        Prefabs prefabs = BuildPrefabs(art, audio);

        var buildScenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(BuildMainMenu(art, prefabs), true)
        };

        foreach (LevelDef level in Levels)
        {
            buildScenes.Add(new EditorBuildSettingsScene(BuildLevel(level, art, prefabs), true));
        }

        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene($"{SceneDir}/{MainMenuScene}.unity");
        Debug.Log("Unfilial: menú principal y 3 niveles generados.");
    }

    // ================================================================== ARTE

    private static Art BuildArt()
    {
        var art = new Art();

        art.PlayerSheet = ImportSheet("player_sheet");
        art.PlayerController = BuildPlayerAnimator(art.PlayerSheet);
        art.BossSheets = Bosses.Select(b => ImportSheet(b.Sheet)).ToArray();
        art.BossControllers = Bosses.Select((b, i) => BuildBossAnimator(art.BossSheets[i], b.Id)).ToArray();
        BuildTileset(art);

        art.Orb = MakeSprite("orb", 32, 32, 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
            if (d >= 1f) return Color.clear;
            float alpha = d < 0.35f ? 1f : Mathf.Pow(1f - (d - 0.35f) / 0.65f, 2f);
            return new Color(1f, 1f, 1f, alpha);
        });

        art.Crawler = MakeSprite("enemy_crawler", 32, 22, TileSize, (x, y) =>
        {
            float u = (x + 0.5f) / 32f, v = (y + 0.5f) / 22f;
            float dome = Mathf.Pow((u - 0.5f) / 0.48f, 2f) + Mathf.Pow((v - 0.08f) / 0.86f, 2f);
            if (dome > 1f || v < 0.06f) return Color.clear;
            if ((x == 20 || x == 21 || x == 25 || x == 26) && (y >= 9 && y <= 11)) return Color.white;
            float rim = dome > 0.78f ? 0.8f : 1f;
            return Gray((0.35f + v * 0.35f) * rim);
        });

        art.Brute = MakeSprite("enemy_brute", 52, 52, TileSize, (x, y) =>
        {
            if (y >= 38 && y < 51)
            {
                float t = (y - 38) / 13f;
                if (Mathf.Abs(x - (10 - t * 3f)) < (1f - t) * 4f) return Gray(0.85f);
                if (Mathf.Abs(x - (42 + t * 3f)) < (1f - t) * 4f) return Gray(0.85f);
            }
            if (!InRoundedRect(x, y, 5, 0, 47, 41, 11)) return Color.clear;
            if (y >= 23 && y <= 25 && ((x >= 29 && x <= 34) || (x >= 38 && x <= 43))) return Color.white;
            bool rim = !InRoundedRect(x, y, 7, 2, 45, 39, 9);
            return Gray(rim ? 0.75f : 0.3f + (y / 52f) * 0.3f);
        });

        art.Shooter = MakeSprite("enemy_vigia", 40, 56, TileSize, (x, y) =>
        {
            // Tótem de piedra con un ojo luminoso
            if (!InRoundedRect(x, y, 6, 0, 34, 50, 8)) return Color.clear;
            float eye = Vector2.Distance(new Vector2(x, y), new Vector2(24, 34));
            if (eye < 3.5f) return Color.white;
            if (eye < 5.5f) return Gray(0.15f);
            bool band = y >= 14 && y <= 17;
            bool rim = !InRoundedRect(x, y, 8, 2, 32, 48, 6);
            return Gray(rim ? 0.75f : band ? 0.55f : 0.32f + (y / 56f) * 0.2f);
        });

        art.Slash = MakeSprite("slash", 26, 58, TileSize, (x, y) =>
        {
            // Media luna ")" de ~0.8 x 1.8 unidades
            float px = (x + 0.5f) / TileSize, py = (y + 0.5f) / TileSize;
            float d1 = Vector2.Distance(new Vector2(px, py), new Vector2(-0.5f, 0.9f));
            float d2 = Vector2.Distance(new Vector2(px, py), new Vector2(-0.78f, 0.9f));
            if (d1 > 1.25f || d2 < 1.25f) return Color.clear;
            float taper = 1f - Mathf.Pow(Mathf.Abs(py - 0.9f) / 0.9f, 2f);
            float edge = Mathf.InverseLerp(1.25f, 1.53f, d2);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(taper * (0.35f + edge * 0.65f)));
        });

        art.Shield = MakeSprite("shield", 20, 52, TileSize, (x, y) =>
        {
            float u = (x + 0.5f - 10f) / 10f, v = (y + 0.5f - 26f) / 26f;
            float d = u * u + v * v;
            if (d > 1f) return Color.clear;
            return new Color(1f, 1f, 1f, d > 0.65f ? 1f : 0.35f);
        });

        art.Gate = MakeSprite("exit_gate", 48, 96, TileSize, (x, y) =>
        {
            bool outer = InArch(x, y, 0, 48, 72, 24);
            bool inner = InArch(x, y, 6, 42, 72, 18);
            if (!outer) return Color.clear;
            if (!inner) return Gray(0.65f);
            return new Color(1f, 1f, 1f, 0.35f + 0.65f * (1f - y / 96f));
        });

        art.UIWhite = MakeSprite("ui_white", 4, 4, 100, (x, y) => Color.white);

        string materialPath = $"{Root}/Art/NoFriction.physicsMaterial2D";
        art.NoFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
        if (art.NoFriction == null)
        {
            art.NoFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(art.NoFriction, materialPath);
        }

        return art;
    }

    private static Color Gray(float v) => new Color(v, v, v, 1f);

    private static bool InRoundedRect(int x, int y, int x0, int y0, int x1, int y1, int r)
    {
        if (x < x0 || x >= x1 || y < y0 || y >= y1) return false;
        float cx = Mathf.Clamp(x, x0 + r, x1 - r);
        float cy = Mathf.Clamp(y, y0 + r, y1 - r);
        return y < y0 + r || Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) <= r; // base plana, esquinas superiores redondeadas
    }

    private static bool InArch(int x, int y, int x0, int x1, int springY, float radius)
    {
        if (x < x0 || x >= x1) return false;
        if (y < springY) return true;
        return Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2((x0 + x1) * 0.5f, springY)) <= radius;
    }

    // ------------------------------------------------------------------ Hojas externas (personaje y jefes) + FSM

    [Serializable] private class SheetSize { public int width; public int height; }
    [Serializable] private class SheetFrame { public string name; public string animation; public int index; public int x; public int y; public int width; public int height; }
    [Serializable] private class SheetData { public SheetSize size; public SheetFrame[] frames; }

    private class SheetAnimation
    {
        public string Name;
        public int Frames;
        public float Fps;
        public bool Loop;
    }

    private class Sheet
    {
        public string Prefix;
        public Dictionary<string, Sprite> Sprites;
        public List<SheetAnimation> Animations;

        public Sprite Frame(string animation, int index) => Sprites[$"{Prefix}_{animation}_{index}"];
    }

    /// <summary>
    /// Importa una hoja PNG generada externamente (Art/Sprites/Characters) y la corta según su JSON.
    /// El JSON usa origen arriba-izquierda; Unity usa abajo-izquierda.
    /// </summary>
    private static Sheet ImportSheet(string baseName)
    {
        string png = $"{CharacterDir}/{baseName}.png";
        string jsonText = File.ReadAllText($"{CharacterDir}/{baseName}.json");
        var data = JsonUtility.FromJson<SheetData>(jsonText);

        ConfigureImporter(png, SpriteImportMode.Multiple, TileSize);
        var rects = data.frames
            .Select(f => (f.name, new Rect(f.x, data.size.height - f.y - f.height, f.width, f.height)))
            .ToList();

        var sheet = new Sheet
        {
            Prefix = baseName == "player_sheet" ? "player" : baseName,
            Sprites = SliceSheet(png, rects),
            Animations = new List<SheetAnimation>(),
        };

        // "animations": { "idle": { "row": 0, "frames": 4, "fps": 6, "loop": true }, ... }
        const string pattern = "\"(\\w+)\"\\s*:\\s*\\{\\s*\"row\"\\s*:\\s*\\d+\\s*,\\s*\"frames\"\\s*:\\s*(\\d+)\\s*,\\s*\"fps\"\\s*:\\s*([\\d.]+)\\s*,\\s*\"loop\"\\s*:\\s*(true|false)";
        foreach (Match match in Regex.Matches(jsonText, pattern))
        {
            sheet.Animations.Add(new SheetAnimation
            {
                Name = match.Groups[1].Value,
                Frames = int.Parse(match.Groups[2].Value),
                Fps = float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                Loop = match.Groups[4].Value == "true",
            });
        }

        return sheet;
    }

    private static AnimationClip CreateClip(Sheet sheet, SheetAnimation animation, string folder, string clipName)
    {
        string path = $"{folder}/{clipName}.anim";
        AssetDatabase.DeleteAsset(path);

        var clip = new AnimationClip { frameRate = animation.Fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[animation.Frames + 1];
        for (int i = 0; i <= animation.Frames; i++)
        {
            // La última clave repite el último frame para que dure un frame completo
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / animation.Fps,
                value = sheet.Frame(animation.Name, Mathf.Min(i, animation.Frames - 1)),
            };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = animation.Loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static RuntimeAnimatorController BuildPlayerAnimator(Sheet sheet)
    {
        var clips = sheet.Animations.ToDictionary(a => a.Name, a => CreateClip(sheet, a, AnimationDir, $"Player_{a.Name}"));

        string controllerPath = $"{AnimationDir}/Player.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("yVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsBlocking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("AttackDir", AnimatorControllerParameterType.Int);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState State(string name, string clip, float x, float y)
        {
            AnimatorState state = machine.AddState(name, new Vector3(x, y));
            state.motion = clips[clip];
            return state;
        }

        AnimatorState sIdle = State("Idle", "idle", 300, 0);
        AnimatorState sRun = State("Run", "run", 300, 120);
        AnimatorState sJump = State("Jump", "jump", 600, 0);
        AnimatorState sFall = State("Fall", "fall", 600, 120);
        AnimatorState sBlock = State("Block", "block", 0, 120);
        AnimatorState sAttack = State("Attack", "attack", 0, 260);
        AnimatorState sAttackUp = State("AttackUp", "attack_up", 200, 260);
        AnimatorState sAttackDown = State("AttackDown", "attack_down", 400, 260);
        AnimatorState sDash = State("Dash", "dash", 600, 260);
        AnimatorState sHurt = State("Hurt", "hurt", 800, 260);
        AnimatorState sDeath = State("Death", "death", 800, 0);
        machine.defaultState = sIdle;

        // Transiciones reactivas: sin Exit Time y duración 0 (cortes nítidos de pixel art)
        AnimatorStateTransition To(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            return t;
        }

        To(sIdle, sRun).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        To(sRun, sIdle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        foreach (AnimatorState grounded in new[] { sIdle, sRun })
        {
            AnimatorStateTransition toJump = To(grounded, sJump);
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            toJump.AddCondition(AnimatorConditionMode.Greater, 0.1f, "yVelocity");

            AnimatorStateTransition toFall = To(grounded, sFall);
            toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            toFall.AddCondition(AnimatorConditionMode.Less, -0.1f, "yVelocity");

            To(grounded, sBlock).AddCondition(AnimatorConditionMode.If, 0f, "IsBlocking");
        }
        To(sBlock, sIdle).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsBlocking");

        To(sJump, sFall).AddCondition(AnimatorConditionMode.Less, 0f, "yVelocity");
        To(sFall, sJump).AddCondition(AnimatorConditionMode.Greater, 0.1f, "yVelocity"); // doble salto

        foreach (AnimatorState air in new[] { sJump, sFall })
        {
            AnimatorStateTransition land = To(air, sIdle);
            land.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            land.AddCondition(AnimatorConditionMode.Less, 0.1f, "yVelocity");
            land.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition landRun = To(air, sRun);
            landRun.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            landRun.AddCondition(AnimatorConditionMode.Less, 0.1f, "yVelocity");
            landRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }

        // Acciones de un pulso desde cualquier estado
        AnimatorStateTransition FromAny(AnimatorState to, string trigger, bool toSelf)
        {
            AnimatorStateTransition t = machine.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.canTransitionToSelf = toSelf;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            return t;
        }

        FromAny(sAttack, "Attack", true).AddCondition(AnimatorConditionMode.Equals, 0f, "AttackDir");
        FromAny(sAttackUp, "Attack", true).AddCondition(AnimatorConditionMode.Equals, 1f, "AttackDir");
        FromAny(sAttackDown, "Attack", true).AddCondition(AnimatorConditionMode.Equals, -1f, "AttackDir");
        FromAny(sDash, "Dash", false);
        FromAny(sHurt, "Hurt", false);
        FromAny(sDeath, "Death", false);

        // Al terminar la acción vuelve a Idle (desde allí reacciona a Run/Jump/Fall en el siguiente frame)
        foreach (AnimatorState action in new[] { sAttack, sAttackUp, sAttackDown, sDash, sHurt })
        {
            AnimatorStateTransition back = action.AddTransition(sIdle);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    /// <summary>
    /// Animator del jefe: un estado por animación (idle, move, attack_melee, attack_special, hurt, phase2, death).
    /// BossController los reproduce directamente con Animator.Play según su IA.
    /// </summary>
    private static RuntimeAnimatorController BuildBossAnimator(Sheet sheet, string id)
    {
        string folder = $"{Root}/Animations/Bosses";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();

        string controllerPath = $"{folder}/Boss_{id}.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        float y = 0f;
        foreach (SheetAnimation animation in sheet.Animations)
        {
            AnimatorState state = machine.AddState(animation.Name, new Vector3(300, y));
            state.motion = CreateClip(sheet, animation, folder, $"Boss_{id}_{animation.Name}");
            if (animation.Name == "idle") machine.defaultState = state;
            y += 70f;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    // ------------------------------------------------------------------ Tileset (hoja Multiple) + Tiles

    private static void BuildTileset(Art art)
    {
        const int columns = 8, rows = 3;
        var sheet = new Color[TileSize * columns * TileSize * rows];
        var rects = new List<(string, Rect)>();
        int slot = 0;

        void AddTile(string name, Func<int, int, Color> painter)
        {
            int ox = (slot % columns) * TileSize;
            int oy = (rows - 1 - slot / columns) * TileSize;
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                    sheet[(oy + y) * TileSize * columns + ox + x] = painter(x, y);
            rects.Add((name, new Rect(ox, oy, TileSize, TileSize)));
            slot++;
        }

        // 16 tiles de suelo: todas las combinaciones de bordes expuestos (auto-tiling por máscara)
        for (int mask = 0; mask < 16; mask++)
        {
            bool top = (mask & 8) != 0, bottom = (mask & 4) != 0, left = (mask & 2) != 0, right = (mask & 1) != 0;
            AddTile($"ground_{mask:00}", (x, y) => GroundPixel(x, y, top, bottom, left, right, false));
        }
        AddTile("ground_cracked", (x, y) => GroundPixel(x, y, false, false, false, false, true));

        AddTile("hazard_spikes", (x, y) =>
        {
            if (y <= 3) return y == 3 ? new Color(0.45f, 0.42f, 0.45f) : new Color(0.3f, 0.28f, 0.3f);
            int local = x % 8;
            float halfWidth = 4f * (1f - (y - 4) / 18f);
            if (halfWidth <= 0f || Mathf.Abs(local + 0.5f - 4f) > halfWidth) return Color.clear;
            bool edge = Mathf.Abs(local + 0.5f - 4f) > halfWidth - 1f;
            Color metal = y > 16 ? new Color(0.95f, 0.75f, 0.75f) : new Color(0.82f, 0.82f, 0.86f);
            return edge ? metal * 0.6f + new Color(0, 0, 0, 0.4f) : metal;
        });

        AddTile("background_wall", (x, y) => WallPixel(x, y, false));
        AddTile("background_wall_alt", (x, y) => WallPixel(x, y, true));

        Dictionary<string, Sprite> sprites = MakeSheet("tileset", TileSize * columns, TileSize * rows, TileSize, sheet, rects);

        art.GroundByMask = new TileBase[16];
        for (int mask = 0; mask < 16; mask++)
        {
            art.GroundByMask[mask] = MakeTile($"ground_{mask:00}", sprites[$"ground_{mask:00}"], Tile.ColliderType.Grid);
        }
        art.GroundCracked = MakeTile("ground_cracked", sprites["ground_cracked"], Tile.ColliderType.Grid);
        art.Spikes = MakeTile("hazard_spikes", sprites["hazard_spikes"], Tile.ColliderType.Sprite);
        art.BackWall = MakeTile("background_wall", sprites["background_wall"], Tile.ColliderType.None);
        art.BackWallAlt = MakeTile("background_wall_alt", sprites["background_wall_alt"], Tile.ColliderType.None);
    }

    private static Color GroundPixel(int x, int y, bool top, bool bottom, bool left, bool right, bool cracked)
    {
        int row = y / 8;
        int bx = (x + (row % 2 == 0 ? 0 : 8)) % 16;
        bool mortar = y % 8 == 0 || bx == 0;
        float noise = Mathf.PerlinNoise(x * 0.23f + 3f, y * 0.23f);
        float v = mortar ? 0.22f : 0.36f + noise * 0.08f;

        if (cracked && (Mathf.Abs(x - y * 0.6f - 6f) < 0.8f || Mathf.Abs(x - 20 + (y - 16) * 0.4f) < 0.7f && y > 10)) v = 0.15f;

        if (left && x <= 1) v = 0.18f;
        else if (left && x == 2) v = Mathf.Max(v, 0.5f);
        if (right && x >= 30) v = 0.18f;
        else if (right && x == 29) v = Mathf.Min(v, 0.28f);
        if (bottom && y <= 1) v = 0.16f;
        else if (bottom && y == 2) v = 0.24f;

        if (top)
        {
            float edge = TileSize - (4f + Mathf.PerlinNoise(x * 0.35f, 7.3f) * 3f);
            if (y >= edge) v = (left && x <= 0) || (right && x >= 31) ? 0.3f : 0.8f + noise * 0.12f;
            else if (y >= edge - 1.5f) v = 0.2f;
        }

        return Gray(v);
    }

    private static Color WallPixel(int x, int y, bool alt)
    {
        int row = y / 8;
        int bx = (x + (row % 2 == 0 ? 0 : 8)) % 16;
        bool mortar = y % 8 == 0 || bx == 0;
        if (alt && x >= 9 && x <= 22 && y >= 9 && y <= 23) return Gray(0.07f); // hueco / ventana
        float noise = Mathf.PerlinNoise(x * 0.15f + 11f, y * 0.15f);
        return mortar ? Gray(0.1f) : Gray(0.17f + noise * 0.05f);
    }

    private static TileBase MakeTile(string name, Sprite sprite, Tile.ColliderType colliderType)
    {
        string path = $"{TileDir}/{name}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = sprite;
        tile.colliderType = colliderType;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void BuildPalette(Art art)
    {
        const string paletteName = "Unfilial_Palette";
        AssetDatabase.DeleteAsset($"{PaletteDir}/{paletteName}.prefab");

        GameObject palette = GridPaletteUtility.CreateNewPalette(PaletteDir, paletteName,
            GridLayout.CellLayout.Rectangle, GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        string path = AssetDatabase.GetAssetPath(palette);

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        Tilemap tilemap = contents.GetComponentInChildren<Tilemap>();

        var tiles = new List<TileBase>(art.GroundByMask) { art.GroundCracked, art.Spikes, art.BackWall, art.BackWallAlt };
        for (int i = 0; i < tiles.Count; i++)
        {
            tilemap.SetTile(new Vector3Int(i % 8, -(i / 8), 0), tiles[i]);
        }

        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ------------------------------------------------------------------ Importación de sprites

    private static Sprite MakeSprite(string name, int width, int height, float pixelsPerUnit, Func<int, int, Color> pixel)
    {
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);

        string path = WriteTexture(name, width, height, pixels);
        ConfigureImporter(path, SpriteImportMode.Single, pixelsPerUnit);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Dictionary<string, Sprite> MakeSheet(string name, int width, int height, float pixelsPerUnit, Color[] pixels, List<(string name, Rect rect)> rects)
    {
        string path = WriteTexture(name, width, height, pixels);
        ConfigureImporter(path, SpriteImportMode.Multiple, pixelsPerUnit);
        return SliceSheet(path, rects);
    }

    private static Dictionary<string, Sprite> SliceSheet(string path, List<(string name, Rect rect)> rects)
    {
        // Corte de la hoja con la API del Sprite Editor (conserva los IDs si ya existían)
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(AssetImporter.GetAtPath(path));
        provider.InitSpriteEditorDataProvider();

        SpriteRect[] existing = provider.GetSpriteRects();
        SpriteRect[] spriteRects = rects.Select(r =>
        {
            SpriteRect previous = existing.FirstOrDefault(e => e.name == r.name);
            return new SpriteRect
            {
                name = r.name,
                rect = r.rect,
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = previous != null ? previous.spriteID : GUID.Generate(),
            };
        }).ToArray();

        provider.SetSpriteRects(spriteRects);
        var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameIds?.SetNameFileIdPairs(spriteRects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)).ToList());
        provider.Apply();
        ((TextureImporter)AssetImporter.GetAtPath(path)).SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
    }

    private static string WriteTexture(string name, int width, int height, Color[] pixels)
    {
        string path = $"{SpriteDir}/{name}.png";
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return path;
    }

    private static void ConfigureImporter(string path, SpriteImportMode mode, float pixelsPerUnit)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = mode;
        importer.spritePixelsPerUnit = pixelsPerUnit;               // PPU alineado al tile (32)
        importer.filterMode = FilterMode.Point;                      // Pixel art nítido
        importer.textureCompression = TextureImporterCompression.Uncompressed; // Sin compresión en desarrollo
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = true;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    // ================================================================== AUDIO

    private class AudioAssets
    {
        public AudioMixer Mixer;
        public AudioMixerGroup MusicGroup, SfxGroup;
        public AudioClip Music;
        public Dictionary<SfxType, AudioClip> Sfx = new Dictionary<SfxType, AudioClip>();
    }

    private static AudioAssets BuildAudio()
    {
        var audio = new AudioAssets();

        // Música: Streaming + Vorbis (ahorra RAM)
        string musicPath = $"{MusicDir}/music_main_loop.wav";
        WavWriter.Write(musicPath, ProceduralAudio.CreateMusicLoop());
        AssetDatabase.ImportAsset(musicPath, ImportAssetOptions.ForceSynchronousImport);
        ConfigureAudio(musicPath, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis);
        audio.Music = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);

        // SFX: Decompress On Load (suenan sin retardo)
        foreach (SfxType type in Enum.GetValues(typeof(SfxType)))
        {
            string path = $"{SfxDir}/sfx_{ToSnakeCase(type.ToString())}.wav";
            WavWriter.Write(path, ProceduralAudio.CreateSfx(type));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureAudio(path, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM);
            audio.Sfx[type] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        audio.Mixer = BuildMixer();
        audio.MusicGroup = audio.Mixer.FindMatchingGroups("Music").FirstOrDefault();
        audio.SfxGroup = audio.Mixer.FindMatchingGroups("SFX").FirstOrDefault();
        return audio;
    }

    private static void ConfigureAudio(string path, AudioClipLoadType loadType, AudioCompressionFormat format)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = loadType;
        settings.compressionFormat = format;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i])) builder.Append('_');
            builder.Append(char.ToLowerInvariant(value[i]));
        }
        return builder.ToString();
    }

    /// <summary>
    /// Crea el MainMixer con grupos Music y SFX y sus volúmenes expuestos (MusicVol / SFXVol).
    /// Unity no ofrece API pública para esto, así que se usa la API interna del editor por reflexión.
    /// </summary>
    private static AudioMixer BuildMixer()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null && existing.FindMatchingGroups("Music").Length > 0 && existing.FindMatchingGroups("SFX").Length > 0)
        {
            return existing;
        }
        AssetDatabase.DeleteAsset(MixerPath);

        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        Assembly editor = typeof(Editor).Assembly;
        Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController");
        Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath");

        object controller = controllerType.GetMethod("CreateMixerControllerAtPath", Any).Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup", Any).GetValue(controller);

        foreach ((string group, string parameter) in new[] { ("Music", "MusicVol"), ("SFX", "SFXVol") })
        {
            object newGroup = controllerType.GetMethod("CreateNewGroup", Any).Invoke(controller, new object[] { group, false });
            controllerType.GetMethod("AddChildToParent", Any).Invoke(controller, new[] { newGroup, master });

            object volumeGuid = newGroup.GetType().GetMethod("GetGUIDForVolume", Any).Invoke(newGroup, null);
            object parameterPath = Activator.CreateInstance(pathType, Any, null, new[] { newGroup, volumeGuid }, null);
            controllerType.GetMethod("AddExposedParameter", Any).Invoke(controller, new[] { parameterPath });

            // Renombrar el parámetro expuesto (por defecto se llama "MyExposedParam")
            PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", Any);
            var exposed = (Array)exposedProperty.GetValue(controller);
            for (int i = 0; i < exposed.Length; i++)
            {
                object entry = exposed.GetValue(i);
                FieldInfo guidField = entry.GetType().GetField("guid", Any);
                if (!guidField.GetValue(entry).Equals(volumeGuid)) continue;

                entry.GetType().GetField("name", Any).SetValue(entry, parameter);
                exposed.SetValue(entry, i);
            }
            exposedProperty.SetValue(controller, exposed);
        }

        controllerType.GetMethod("OnSubAssetChanged", Any)?.Invoke(controller, null);
        EditorUtility.SetDirty((Object)controller);
        AssetDatabase.SaveAssets();
        return (AudioMixer)controller;
    }

    // ================================================================== PREFABS

    private static Prefabs BuildPrefabs(Art art, AudioAssets audio)
    {
        var prefabs = new Prefabs();
        prefabs.HitVfx = SavePrefab(CreateHitVfx(art), VfxPrefabDir, "VFX_HitImpact");
        prefabs.Projectile = SavePrefab(CreateProjectile(art, "Projectile", new Color(0.55f, 1f, 0.9f), 0.7f, 7f, 15), PrefabDir, "Projectile");
        prefabs.Player = SavePrefab(CreatePlayer(art, prefabs.HitVfx), PrefabDir, "Player");
        prefabs.Crawler = SavePrefab(CreateMeleeEnemy(art, "Enemy_Crawler", art.Crawler, CrawlerTint, new Vector2(0.9f, 0.6f), Vector2.zero,
            health: 3, orbs: 1, knockback: 6f, speed: 1.8f, chaseSpeed: 3.6f, detection: 6f,
            attackRange: 2.5f, windUp: 0.45f, dashSpeed: 11f, dashDuration: 0.3f, cooldown: 1.6f, contact: 10, dashDamage: 20, mass: 1f), PrefabDir, "Enemy_Crawler");
        prefabs.Brute = SavePrefab(CreateMeleeEnemy(art, "Enemy_Brute", art.Brute, BruteTint, new Vector2(1.3f, 1.2f), new Vector2(0f, -0.2f),
            health: 8, orbs: 3, knockback: 3f, speed: 1.2f, chaseSpeed: 2.6f, detection: 7f,
            attackRange: 3.2f, windUp: 0.6f, dashSpeed: 14f, dashDuration: 0.35f, cooldown: 2.2f, contact: 15, dashDamage: 30, mass: 3f), PrefabDir, "Enemy_Brute");
        prefabs.Shooter = SavePrefab(CreateShooter(art, prefabs.Projectile), PrefabDir, "Enemy_Vigia");
        prefabs.Orb = SavePrefab(CreateOrb(art), PrefabDir, "Orb");
        prefabs.Exit = SavePrefab(CreateExit(art), PrefabDir, "LevelExit");
        prefabs.Canvas = SavePrefab(CreateGameCanvas(art), PrefabDir, "GameCanvas");
        prefabs.GameManager = SavePrefab(new GameObject("GameManager", typeof(GameManager)), PrefabDir, "GameManager");
        prefabs.AudioManager = SavePrefab(CreateAudioManager(audio), PrefabDir, "AudioManager");

        prefabs.Bosses = new GameObject[Bosses.Length];
        for (int i = 0; i < Bosses.Length; i++)
        {
            BossDef boss = Bosses[i];
            GameObject projectile = SavePrefab(CreateProjectile(art, $"Projectile_{boss.Id}", boss.ProjectileColor, boss.ProjectileScale,
                boss.ProjectileSpeed, boss.ProjectileDamage), PrefabDir, $"Projectile_{boss.Id}");
            prefabs.Bosses[i] = SavePrefab(CreateBoss(art, boss, i, projectile), PrefabDir, $"Boss_{boss.Id}");
        }
        return prefabs;
    }

    private static GameObject SavePrefab(GameObject go, string folder, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreateAudioManager(AudioAssets audio)
    {
        var go = new GameObject("AudioManager");
        var manager = go.AddComponent<AudioManager>();

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("mixer").objectReferenceValue = audio.Mixer;
        serialized.FindProperty("musicGroup").objectReferenceValue = audio.MusicGroup;
        serialized.FindProperty("sfxGroup").objectReferenceValue = audio.SfxGroup;
        serialized.FindProperty("musicClip").objectReferenceValue = audio.Music;

        SerializedProperty list = serialized.FindProperty("sfxClips");
        list.arraySize = audio.Sfx.Count;
        int index = 0;
        foreach (KeyValuePair<SfxType, AudioClip> pair in audio.Sfx)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(index++);
            element.FindPropertyRelative("type").enumValueIndex = (int)pair.Key;
            element.FindPropertyRelative("clip").objectReferenceValue = pair.Value;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    private static GameObject CreateHitVfx(Art art)
    {
        string materialPath = $"{VfxDir}/VFX_Particle.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.mainTexture = art.Orb.texture;
        EditorUtility.SetDirty(material);

        var go = new GameObject("VFX_HitImpact");
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.3f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Gold);
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy; // Se autodestruye al terminar

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 12;
        return go;
    }

    private static GameObject CreateProjectile(Art art, string name, Color color, float scale, float speed, int damage)
    {
        var go = new GameObject(name);
        go.transform.localScale = Vector3.one * scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = art.Orb;
        renderer.color = color;
        renderer.sortingOrder = 9;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
        rb.gravityScale = 0f;

        var circle = go.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.2f;

        var projectile = go.AddComponent<Projectile>();
        Set(projectile, ("groundLayer", Mask(GroundLayer)), ("speed", speed), ("damage", damage));
        return go;
    }

    private static GameObject CreatePlayer(Art art, GameObject hitVfx)
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.layer = PlayerLayer;

        var renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = art.PlayerSheet.Frame("idle", 0);
        renderer.sortingOrder = 10;

        var animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = art.PlayerController;

        var rb = player.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 3.5f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sharedMaterial = art.NoFriction; // Evita quedarse pegado a las paredes

        var capsule = player.AddComponent<CapsuleCollider2D>();
        // Silueta medida en la hoja: pies en y=-0.91, cabeza (sin cuernos) en ~0.75
        capsule.size = new Vector2(0.6f, 1.65f);
        capsule.offset = new Vector2(0f, -0.075f);

        Transform groundCheck = Child(player, "GroundCheck", new Vector2(0f, -0.92f));
        Transform attackPoint = Child(player, "AttackPoint", new Vector2(0.95f, 0.1f));
        // La hoja ya dibuja el tajo y el escudo: no hacen falta los efectos superpuestos (SlashFX / ShieldFX)

        player.AddComponent<PlayerInput>();
        var movement = player.AddComponent<PlayerMovement>();
        Set(movement,
            ("moveSpeed", 8f), ("jumpForce", 14f), ("maxJumps", 2), ("fallGravityMultiplier", 2.5f),
            ("dashSpeed", 24f), ("dashDuration", 0.2f), ("dashCooldown", 1f),
            ("groundCheck", groundCheck), ("groundCheckRadius", 0.2f), ("groundLayer", Mask(GroundLayer)));

        var shield = player.AddComponent<PlayerShield>();

        player.AddComponent<PlayerHealth>();

        var attack = player.AddComponent<PlayerAttack>();
        Set(attack, ("attackPoint", attackPoint), ("enemyLayer", Mask(EnemyLayer)), ("verticalAttackOffset", 1.2f),
            ("shield", shield), ("hitImpactVfx", hitVfx.GetComponent<ParticleSystem>()));

        player.AddComponent<PlayerAnimator>();
        player.AddComponent<PlayerAudio>();

        var impulse = player.AddComponent<CinemachineImpulseSource>();
        impulse.ImpulseDefinition.ImpulseDuration = 0.2f;
        impulse.DefaultVelocity = new Vector3(0.4f, -1f, 0f);
        player.AddComponent<PlayerCameraShake>();

        return player;
    }

    private static GameObject CreateMeleeEnemy(Art art, string name, Sprite sprite, Color tint, Vector2 colliderSize, Vector2 colliderOffset,
        int health, int orbs, float knockback, float speed, float chaseSpeed, float detection,
        float attackRange, float windUp, float dashSpeed, float dashDuration, float cooldown, int contact, int dashDamage, float mass)
    {
        GameObject enemy = EnemyBase(name, sprite, tint, colliderSize, colliderOffset, mass, art.NoFriction);

        float halfWidth = colliderSize.x * 0.5f;
        float halfHeight = colliderSize.y * 0.5f - colliderOffset.y;

        var enemyHealth = enemy.AddComponent<EnemyHealth>();
        Set(enemyHealth, ("maxHealth", health), ("orbReward", orbs), ("knockbackForce", knockback));

        var patrol = enemy.AddComponent<EnemyPatrol>();
        Set(patrol,
            ("moveSpeed", speed), ("chaseSpeed", chaseSpeed), ("detectionRadius", detection), ("loseInterestRadius", detection * 1.5f),
            ("maxVerticalChaseDistance", 3f), ("playerLayer", Mask(PlayerLayer)), ("groundLayer", Mask(GroundLayer)),
            ("edgeCheckDistance", halfHeight + 0.6f), ("wallCheckDistance", halfWidth + 0.15f),
            ("edgeCheckOffset", new Vector2(halfWidth + 0.05f, 0f)));

        var attacker = enemy.AddComponent<EnemyMeleeAttacker>();
        Set(attacker,
            ("attackRange", attackRange), ("playerLayer", Mask(PlayerLayer)), ("windUpTime", windUp), ("dashSpeed", dashSpeed),
            ("dashDuration", dashDuration), ("attackCooldown", cooldown), ("contactDamage", contact), ("dashDamage", dashDamage));

        return enemy;
    }

    private static GameObject CreateShooter(Art art, GameObject projectile)
    {
        // Sin material sin fricción: el knockback no lo desliza indefinidamente
        GameObject enemy = EnemyBase("Enemy_Vigia", art.Shooter, ShooterTint, new Vector2(0.85f, 1.6f), new Vector2(0f, -0.07f), 5f, null);

        var enemyHealth = enemy.AddComponent<EnemyHealth>();
        Set(enemyHealth, ("maxHealth", 3), ("orbReward", 2), ("knockbackForce", 1.5f));

        Transform firePoint = Child(enemy, "FirePoint", new Vector2(0.55f, 0.35f));

        var pool = enemy.AddComponent<ProjectilePool>();
        Set(pool, ("projectilePrefab", projectile.GetComponent<Projectile>()));

        var shooter = enemy.AddComponent<EnemyShooter>();
        Set(shooter, ("playerLayer", Mask(PlayerLayer)), ("firePoint", firePoint));
        return enemy;
    }

    private static GameObject CreateBoss(Art art, BossDef def, int index, GameObject projectile)
    {
        var boss = new GameObject($"Boss_{def.Id}");
        boss.layer = EnemyLayer;

        var renderer = boss.AddComponent<SpriteRenderer>();
        renderer.sprite = art.BossSheets[index].Frame("idle", 0);
        renderer.sortingOrder = 9;

        var animator = boss.AddComponent<Animator>();
        animator.runtimeAnimatorController = art.BossControllers[index];

        var rb = boss.AddComponent<Rigidbody2D>();
        rb.mass = 50f;
        rb.gravityScale = def.Flying ? 0f : 3f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sharedMaterial = art.NoFriction;

        var box = boss.AddComponent<BoxCollider2D>();
        box.size = def.ColliderSize;
        box.offset = def.ColliderOffset;

        var health = boss.AddComponent<BossHealth>();
        Set(health, ("maxHealth", def.Health), ("orbReward", def.Orbs));

        var pool = boss.AddComponent<ProjectilePool>();
        Set(pool, ("projectilePrefab", projectile.GetComponent<Projectile>()), ("maxSize", 30));

        var impulse = boss.AddComponent<CinemachineImpulseSource>();
        impulse.ImpulseDefinition.ImpulseDuration = 0.3f;

        Transform firePoint = Child(boss, "FirePoint", def.FirePoint);

        var controller = boss.AddComponent<BossController>();
        Set(controller,
            ("bossName", def.Name), ("special", (int)def.Special), ("playerLayer", Mask(PlayerLayer)), ("groundLayer", Mask(GroundLayer)),
            ("moveSpeed", def.Speed), ("isFlying", def.Flying), ("meleeRange", def.MeleeRange), ("lungeSpeed", def.LungeSpeed),
            ("strikeBoxSize", def.StrikeBox), ("firePoint", firePoint));
        return boss;
    }

    private static GameObject EnemyBase(string name, Sprite sprite, Color tint, Vector2 colliderSize, Vector2 colliderOffset, float mass, PhysicsMaterial2D material)
    {
        var enemy = new GameObject(name);
        enemy.layer = EnemyLayer;

        var renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = 8;

        var rb = enemy.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.mass = mass;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sharedMaterial = material;

        var box = enemy.AddComponent<BoxCollider2D>();
        box.size = colliderSize;
        box.offset = colliderOffset;
        return enemy;
    }

    private static GameObject CreateOrb(Art art)
    {
        var orb = new GameObject("Orb");
        orb.transform.localScale = Vector3.one * 0.9f;
        var renderer = orb.AddComponent<SpriteRenderer>();
        renderer.sprite = art.Orb;
        renderer.color = OrbTint;
        renderer.sortingOrder = 5;

        var circle = orb.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = 0.35f;

        orb.AddComponent<OrbPickup>();
        return orb;
    }

    private static GameObject CreateExit(Art art)
    {
        var exit = new GameObject("LevelExit");
        var renderer = exit.AddComponent<SpriteRenderer>();
        renderer.sprite = art.Gate;
        renderer.color = new Color(1f, 0.92f, 0.7f);
        renderer.sortingOrder = 2;

        var box = exit.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1f, 2.6f);

        var transition = exit.AddComponent<RoomTransition>();
        Set(transition, ("mode", 1)); // CompleteLevel
        return exit;
    }

    private static Transform Child(GameObject parent, string name, Vector2 localPosition)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent.transform, false);
        child.localPosition = localPosition;
        return child;
    }

    // ================================================================== UI

    private static GameObject CreateGameCanvas(Art art)
    {
        GameObject canvas = CreateCanvas("GameCanvas", 10);

        // Panel HUD
        GameObject hud = NewUI("HUD", canvas.transform);
        Stretch(hud);

        // Barra de vida: fondo + Image Filled (Horizontal, origen Left), esquina superior izquierda
        GameObject healthBar = NewUI("HealthBar", hud.transform);
        Place(healthBar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -50f), new Vector2(440f, 30f));
        var healthBackground = healthBar.AddComponent<Image>();
        healthBackground.sprite = art.UIWhite;
        healthBackground.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
        healthBackground.raycastTarget = false;

        GameObject fillGo = NewUI("Fill", healthBar.transform);
        Stretch(fillGo);
        ((RectTransform)fillGo.transform).offsetMin = new Vector2(4f, 4f);
        ((RectTransform)fillGo.transform).offsetMax = new Vector2(-4f, -4f);
        var healthFill = fillGo.AddComponent<Image>();
        healthFill.sprite = art.UIWhite;
        healthFill.color = new Color(0.92f, 0.92f, 1f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.fillAmount = 1f;
        healthFill.raycastTarget = false;

        // Contador de orbes (TMP Auto Size), esquina superior derecha
        TextMeshProUGUI orbs = Label("OrbsCounter", hud.transform, "Orbes: 0", 44f, Gold, TextAlignmentOptions.Right);
        orbs.enableAutoSizing = true;
        orbs.fontSizeMin = 20f;
        orbs.fontSizeMax = 48f;
        Place(orbs.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -38f), new Vector2(460f, 60f));

        TextMeshProUGUI power = Label("PowerText", hud.transform, "Poder Nv. 1", 30f, new Color(0.6f, 0.85f, 1f), TextAlignmentOptions.Right);
        power.enableAutoSizing = true;
        power.fontSizeMin = 16f;
        power.fontSizeMax = 32f;
        Place(power.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -100f), new Vector2(460f, 44f));

        // Barra de vida del jefe (abajo, centrada)
        GameObject bossBar = NewUI("BossBar", hud.transform);
        Place(bossBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1100f, 80f));
        TextMeshProUGUI bossName = Label("BossName", bossBar.transform, "JEFE", 32f, Gold);
        bossName.characterSpacing = 8f;
        Place(bossName.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1100f, 44f));
        GameObject bossBackground = NewUI("Background", bossBar.transform);
        Place(bossBackground, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1100f, 26f));
        var bossBackgroundImage = bossBackground.AddComponent<Image>();
        bossBackgroundImage.sprite = art.UIWhite;
        bossBackgroundImage.color = new Color(0.08f, 0.05f, 0.07f, 0.9f);
        bossBackgroundImage.raycastTarget = false;
        GameObject bossFillGo = NewUI("Fill", bossBackground.transform);
        Stretch(bossFillGo);
        ((RectTransform)bossFillGo.transform).offsetMin = new Vector2(4f, 4f);
        ((RectTransform)bossFillGo.transform).offsetMax = new Vector2(-4f, -4f);
        var bossFill = bossFillGo.AddComponent<Image>();
        bossFill.sprite = art.UIWhite;
        bossFill.color = new Color(0.85f, 0.22f, 0.3f);
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        bossFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        bossFill.raycastTarget = false;
        bossBar.SetActive(false);

        TextMeshProUGUI title = Label("LevelTitle", canvas.transform, "NIVEL", 84f, Color.white);
        title.characterSpacing = 8f;
        Place(title.gameObject, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 260f));

        TextMeshProUGUI toast = Label("Toast", canvas.transform, "", 46f, Gold);
        Place(toast.gameObject, new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 80f));

        // Flash de daño (por encima del HUD, por debajo de los paneles)
        Image flash = FullPanel("DamageFlash", canvas.transform, new Color(0.85f, 0f, 0f, 0f));
        flash.raycastTarget = false;
        flash.gameObject.AddComponent<DamageFlash>();

        Image gameOver = FullPanel("GameOverPanel", canvas.transform, new Color(0f, 0f, 0f, 0.75f));
        TextMeshProUGUI gameOverText = Label("GameOverText", gameOver.transform, "HAS CAÍDO", 130f, new Color(0.95f, 0.9f, 0.92f));
        gameOverText.characterSpacing = 14f;
        Place(gameOverText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1600f, 200f));
        TextMeshProUGUI retry = Label("RetryText", gameOver.transform, "Reintentando...", 32f, new Color(0.7f, 0.7f, 0.75f));
        Place(retry.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(800f, 60f));
        gameOver.gameObject.SetActive(false);

        Image levelComplete = FullPanel("LevelCompletePanel", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
        TextMeshProUGUI completeText = Label("LevelCompleteText", levelComplete.transform, "NIVEL COMPLETADO", 100f, Gold);
        completeText.characterSpacing = 10f;
        Place(completeText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 200f));
        levelComplete.gameObject.SetActive(false);

        // Menú de pausa (anclado al centro)
        Color menuBackground = new Color(0.02f, 0.02f, 0.05f, 0.85f);
        Image pausePanel = FullPanel("PausaPanel", canvas.transform, menuBackground);
        TextMeshProUGUI pauseTitle = Label("Title", pausePanel.transform, "PAUSA", 100f, Color.white);
        pauseTitle.characterSpacing = 20f;
        Place(pauseTitle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1000f, 160f));
        Transform pauseColumn = Column("Buttons", pausePanel.transform, new Vector2(0f, 110f));
        Button resume = MenuButton(pauseColumn, "Seguir Jugando");
        Button pauseSound = MenuButton(pauseColumn, "Sonido");
        Button quit = MenuButton(pauseColumn, "Salir");
        ControlsFooter(pausePanel.transform);
        pausePanel.gameObject.SetActive(false);

        Image soundPanel = FullPanel("SoundPanel", canvas.transform, menuBackground);
        Button soundBack = BuildSoundContent(soundPanel.gameObject, headerY: 250f, headerSize: 100f);
        soundPanel.gameObject.SetActive(false);

        var ui = canvas.AddComponent<UIManager>();
        Set(ui, ("healthFill", healthFill), ("orbsText", orbs), ("powerText", power), ("levelTitleText", title),
            ("toastText", toast), ("gameOverPanel", gameOver.gameObject), ("levelCompletePanel", levelComplete.gameObject),
            ("bossBar", bossBar), ("bossHealthFill", bossFill), ("bossNameText", bossName));

        var pause = canvas.AddComponent<PauseMenu>();
        Set(pause, ("pausePanel", pausePanel.gameObject), ("soundPanel", soundPanel.gameObject), ("resumeButton", resume),
            ("soundButton", pauseSound), ("quitButton", quit), ("soundBackButton", soundBack), ("mainMenuScene", MainMenuScene));

        return canvas;
    }

    private static Button BuildSoundContent(GameObject panel, float headerY, float headerSize)
    {
        TextMeshProUGUI header = Label("Title", panel.transform, "SONIDO", headerSize, Color.white);
        header.characterSpacing = headerSize > 50f ? 20f : 10f;
        Place(header.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, headerY), new Vector2(1000f, headerSize * 1.6f));

        float rowY = headerY - (headerSize > 50f ? 170f : 110f);
        Slider music = VolumeRow(panel.transform, "Música", rowY, out TextMeshProUGUI musicValue);
        Slider sfx = VolumeRow(panel.transform, "Efectos (SFX)", rowY - 100f, out TextMeshProUGUI sfxValue);

        Transform column = Column("Buttons", panel.transform, new Vector2(0f, rowY - 190f));
        Button back = MenuButton(column, "Volver");

        var volumePanel = panel.AddComponent<VolumeSettingsPanel>();
        Set(volumePanel, ("musicSlider", music), ("sfxSlider", sfx), ("musicValueText", musicValue), ("sfxValueText", sfxValue));
        return back;
    }

    private static Slider VolumeRow(Transform parent, string label, float y, out TextMeshProUGUI valueText)
    {
        TextMeshProUGUI text = Label(label + "Label", parent, label, 40f, new Color(0.85f, 0.86f, 0.92f), TextAlignmentOptions.Right);
        Place(text.gameObject, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, y), new Vector2(420f, 60f));

        Slider slider = CreateSlider(label + "Slider", parent, new Color(0.2f, 0.22f, 0.28f), Gold);
        Place(slider.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-110f, y), new Vector2(420f, 22f));

        valueText = Label(label + "Value", parent, "100%", 36f, Gold, TextAlignmentOptions.Left);
        Place(valueText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(340f, y), new Vector2(160f, 60f));
        return slider;
    }

    private static GameObject CreateCanvas(string name, int sortingOrder)
    {
        var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = UILayer;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasGo;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(GameObject go)
    {
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image FullPanel(string name, Transform parent, Color color)
    {
        GameObject go = NewUI(name, parent);
        Stretch(go);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject go = NewUI(name, parent);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Transform Column(string name, Transform parent, Vector2 topPosition)
    {
        GameObject go = NewUI(name, parent);
        Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), topPosition, new Vector2(800f, 600f));

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return go.transform;
    }

    private static Button MenuButton(Transform parent, string text, float fontSize = 50f)
    {
        GameObject go = NewUI(text, parent);
        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = fontSize * 1.6f;

        var hitArea = go.AddComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0f); // Invisible pero recibe clics

        TextMeshProUGUI label = Label("Label", go.transform, text, fontSize, Color.white);
        Stretch(label.gameObject);

        var button = go.AddComponent<Button>();
        button.targetGraphic = label;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = new Color(0.75f, 0.77f, 0.85f);
        colors.highlightedColor = Gold;
        colors.selectedColor = Gold;
        colors.pressedColor = new Color(0.85f, 0.6f, 0.25f);
        colors.disabledColor = new Color(0.4f, 0.4f, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    private static Slider CreateSlider(string name, Transform parent, Color background, Color fill)
    {
        GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = UILayer;

        var slider = go.GetComponent<Slider>();
        go.transform.Find("Background").GetComponent<Image>().color = background;

        var fillArea = (RectTransform)go.transform.Find("Fill Area");
        fillArea.offsetMin = Vector2.zero;
        fillArea.offsetMax = Vector2.zero;
        var fillRect = (RectTransform)fillArea.Find("Fill");
        fillRect.sizeDelta = Vector2.zero;
        fillRect.GetComponent<Image>().color = fill;

        var handle = (RectTransform)go.transform.Find("Handle Slide Area/Handle");
        handle.sizeDelta = new Vector2(22f, 16f);
        handle.GetComponent<Image>().color = Color.white;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private static void ControlsFooter(Transform parent)
    {
        TextMeshProUGUI hint = Label("ControlsHint", parent, ControlsHint, 24f, new Color(0.6f, 0.62f, 0.7f));
        Place(hint.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1850f, 50f));
    }

    // ================================================================== ESCENAS

    private static string BuildMainMenu(Art art, Prefabs prefabs)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(new Color(0.035f, 0.04f, 0.07f), 7f);
        PrefabUtility.InstantiatePrefab(prefabs.AudioManager, scene);
        CreateEventSystem();

        GameObject canvas = CreateCanvas("MainMenuCanvas", 0);

        var glowGo = NewUI("TitleGlow", canvas.transform);
        Place(glowGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(1300f, 1300f));
        var glow = glowGo.AddComponent<Image>();
        glow.sprite = art.Orb;
        glow.color = new Color(0.45f, 0.6f, 1f, 0.12f);
        glow.raycastTarget = false;

        TextMeshProUGUI title = Label("Title", canvas.transform, "UNFILIAL", 170f, new Color(0.93f, 0.94f, 1f));
        title.characterSpacing = 30f;
        Place(title.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1800f, 240f));

        var dividerGo = NewUI("Divider", canvas.transform);
        Place(dividerGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(420f, 3f));
        var divider = dividerGo.AddComponent<Image>();
        divider.color = new Color(Gold.r, Gold.g, Gold.b, 0.7f);
        divider.raycastTarget = false;

        GameObject mainPanel = NewUI("MainPanel", canvas.transform);
        Stretch(mainPanel);
        Transform mainColumn = Column("Buttons", mainPanel.transform, new Vector2(0f, 110f));
        Button play = MenuButton(mainColumn, "Jugar");
        Button levels = MenuButton(mainColumn, "Menú");
        Button sound = MenuButton(mainColumn, "Sonido");
        Button quit = MenuButton(mainColumn, "Salir");

        GameObject levelsPanel = NewUI("LevelsPanel", canvas.transform);
        Stretch(levelsPanel);
        TextMeshProUGUI levelsHeader = Label("Header", levelsPanel.transform, "SELECCIONA UN NIVEL", 34f, Gold);
        levelsHeader.characterSpacing = 10f;
        Place(levelsHeader.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1000f, 60f));
        Transform levelsColumn = Column("Buttons", levelsPanel.transform, new Vector2(0f, 70f));
        var levelButtons = new Button[Levels.Length];
        var levelScenes = new string[Levels.Length];
        for (int i = 0; i < Levels.Length; i++)
        {
            levelButtons[i] = MenuButton(levelsColumn, $"{Levels[i].Number}  ·  {Levels[i].Name}", 44f);
            levelScenes[i] = Levels[i].Scene;
        }
        Button levelsBack = MenuButton(levelsColumn, "Volver", 44f);
        levelsPanel.SetActive(false);

        GameObject soundPanel = NewUI("SoundPanel", canvas.transform);
        Stretch(soundPanel);
        Button soundBack = BuildSoundContent(soundPanel, headerY: 120f, headerSize: 34f);
        soundPanel.SetActive(false);

        ControlsFooter(canvas.transform);

        var controller = canvas.AddComponent<MainMenuController>();
        Set(controller,
            ("mainPanel", mainPanel), ("levelsPanel", levelsPanel), ("soundPanel", soundPanel),
            ("playButton", play), ("levelsButton", levels), ("soundButton", sound), ("quitButton", quit),
            ("levelButtons", levelButtons), ("levelScenes", levelScenes), ("levelsBackButton", levelsBack),
            ("soundBackButton", soundBack));

        string path = $"{SceneDir}/{MainMenuScene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    private static string BuildLevel(LevelDef level, Art art, Prefabs prefabs)
    {
        string[] lines = File.ReadAllLines($"{LevelDir}/{level.Scene}.txt");
        int height = lines.Length;
        int width = lines.Max(l => l.Length);

        char Cell(int x, int y)
        {
            if (y < 0 || y >= height) return ' ';
            string line = lines[height - 1 - y]; // La primera línea del archivo es la fila superior
            return x >= 0 && x < line.Length ? line[x] : ' ';
        }

        // Fuera del mapa cuenta como sólido para no dibujar bordes en los límites
        bool hasBoss = lines.Any(l => l.Contains('K'));

        bool Solid(int x, int y) => x < 0 || x >= width || y < 0 || y >= height || Cell(x, y) == '#';

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ReloadTiles(art); // Después de NewScene: al crear la escena se descargan los assets no referenciados

        Camera camera = CreateCamera(level.Sky, 7f);
        camera.gameObject.AddComponent<CinemachineBrain>();

        var lightGo = new GameObject("Global Light 2D");
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        // --- Grid y Tilemaps (Cell Size 1 x 1)
        var gridGo = new GameObject("Grid", typeof(Grid));
        gridGo.GetComponent<Grid>().cellSize = Vector3.one;

        Tilemap background = CreateTilemap(gridGo, "Background_Tilemap", -10, level.Background);
        for (int x = 0; x < width; x++)
            for (int y = -3; y < height; y++)
                background.SetTile(new Vector3Int(x, y, 0), Hash(x, y) % 37 == 0 ? art.BackWallAlt : art.BackWall);

        Tilemap ground = CreateTilemap(gridGo, "Ground_Tilemap", 0, level.Ground);
        ground.gameObject.layer = GroundLayer;
        var groundBody = ground.gameObject.AddComponent<Rigidbody2D>();
        groundBody.bodyType = RigidbodyType2D.Static; // Obligatorio: el suelo no cae por gravedad
        var groundCollider = ground.gameObject.AddComponent<TilemapCollider2D>();
        groundCollider.compositeOperation = Collider2D.CompositeOperation.Merge; // "Used By Composite"
        var composite = ground.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        Tilemap hazards = CreateTilemap(gridGo, "Hazards_Tilemap", 1, Color.white);
        var hazardCollider = hazards.gameObject.AddComponent<TilemapCollider2D>();
        hazardCollider.isTrigger = true;
        hazards.gameObject.AddComponent<DamageZone>();

        // --- Contenido
        var enemies = new GameObject("Enemies").transform;
        var orbs = new GameObject("Orbs").transform;
        GameObject player = null;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                switch (Cell(x, y))
                {
                    case '#':
                        int mask = (Solid(x, y + 1) ? 0 : 8) | (Solid(x, y - 1) ? 0 : 4) | (Solid(x - 1, y) ? 0 : 2) | (Solid(x + 1, y) ? 0 : 1);
                        TileBase tile = mask == 0 && Hash(x, y) % 7 == 0 ? art.GroundCracked : art.GroundByMask[mask];
                        ground.SetTile(new Vector3Int(x, y, 0), tile);
                        break;
                    case '^':
                        hazards.SetTile(new Vector3Int(x, y, 0), art.Spikes);
                        break;
                    case 'P':
                        player = Spawn(prefabs.Player, scene, null, new Vector2(x + 0.5f, y + 1.02f));
                        break;
                    case 'E':
                        Spawn(prefabs.Crawler, scene, enemies, new Vector2(x + 0.5f, y + 0.32f));
                        break;
                    case 'B':
                        Spawn(prefabs.Brute, scene, enemies, new Vector2(x + 0.5f, y + 0.82f));
                        break;
                    case 'S':
                        Spawn(prefabs.Shooter, scene, enemies, new Vector2(x + 0.5f, y + 0.9f));
                        break;
                    case 'o':
                        Spawn(prefabs.Orb, scene, orbs, new Vector2(x + 0.5f, y + 0.5f));
                        break;
                    case 'K':
                        Spawn(prefabs.Bosses[level.Boss], scene, null, new Vector2(x + 0.5f, y + Bosses[level.Boss].SpawnHeight));
                        break;
                    case 'X':
                        GameObject exit = Spawn(prefabs.Exit, scene, null, new Vector2(x + 0.5f, y + 1.5f));
                        if (hasBoss) exit.AddComponent<ExitGate>(); // Sellado hasta derrotar al jefe
                        break;
                }
            }
        }

        if (player == null) Debug.LogError($"{level.Scene}: el mapa no tiene 'P' (spawn del jugador).");

        // Los cambios de tiles se procesan en diferido: forzamos la generación de colisiones
        // para que la geometría del Composite se guarde en la escena (si no, se guardaría vacía).
        groundCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();
        hazardCollider.ProcessTilemapChanges();

        // --- Vacío bajo el nivel
        var fallZone = new GameObject("FallZone");
        fallZone.transform.position = new Vector3(width * 0.5f, -5f, 0f);
        var fallBox = fallZone.AddComponent<BoxCollider2D>();
        fallBox.isTrigger = true;
        fallBox.size = new Vector2(width + 20f, 2f);
        fallZone.AddComponent<FallZone>();

        // --- Cámara virtual con límites del nivel (Confiner 2D) y sacudida (Impulse Listener)
        var bounds = new GameObject("CameraBounds");
        bounds.layer = LayerMask.NameToLayer("Ignore Raycast");
        var boundsShape = bounds.AddComponent<PolygonCollider2D>();
        boundsShape.isTrigger = true;
        boundsShape.pathCount = 1;
        boundsShape.SetPath(0, new[]
        {
            new Vector2(0f, -3f), new Vector2(width, -3f), new Vector2(width, height), new Vector2(0f, height)
        });

        var vcamGo = new GameObject("CM vcam1");
        var vcam = vcamGo.AddComponent<CinemachineCamera>();
        LensSettings lens = LensSettings.Default;
        lens.OrthographicSize = 7f;
        vcam.Lens = lens;
        if (player != null)
        {
            vcam.Follow = player.transform;
            vcamGo.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
        }

        var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
        composer.CameraDistance = 10f;
        composer.Damping = new Vector3(0.6f, 0.4f, 0f); // Vertical más firme que horizontal
        composer.Lookahead = new LookaheadSettings { Enabled = true, Time = 0.15f, Smoothing = 5f, IgnoreY = true };
        ScreenComposerSettings composition = composer.Composition;
        composition.DeadZone.Enabled = true;
        composition.DeadZone.Size = new Vector2(0.15f, 0.1f); // Evita mareos por micro-movimientos
        composer.Composition = composition;

        var confiner = vcamGo.AddComponent<CinemachineConfiner2D>();
        confiner.BoundingShape2D = boundsShape;

        var listener = vcamGo.AddComponent<CinemachineImpulseListener>();
        listener.Use2DDistance = true;

        // --- Sistemas
        PrefabUtility.InstantiatePrefab(prefabs.GameManager, scene);
        PrefabUtility.InstantiatePrefab(prefabs.AudioManager, scene);
        new GameObject("_SceneFlow", typeof(SceneFlowController));
        CreateEventSystem();

        var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Canvas, scene);
        var title = canvas.transform.Find("LevelTitle").GetComponent<TextMeshProUGUI>();
        title.text = $"<size=45%><color=#FFD98A>{level.Number}</color></size>\n{level.Name.ToUpperInvariant()}";
        PrefabUtility.RecordPrefabInstancePropertyModifications(title);

        string path = $"{SceneDir}/{level.Scene}.unity";
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    /// <summary>
    /// NewScene (modo Single) descarga los assets sin referencias, invalidando los Tile en memoria;
    /// se recargan desde disco antes de pintar cada nivel.
    /// </summary>
    private static void ReloadTiles(Art art)
    {
        TileBase Load(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TileDir}/{name}.asset");

        for (int mask = 0; mask < 16; mask++) art.GroundByMask[mask] = Load($"ground_{mask:00}");
        art.GroundCracked = Load("ground_cracked");
        art.Spikes = Load("hazard_spikes");
        art.BackWall = Load("background_wall");
        art.BackWallAlt = Load("background_wall_alt");
    }

    private static int Hash(int x, int y) => ((x * 73856093) ^ (y * 19349663)) & int.MaxValue;

    private static Tilemap CreateTilemap(GameObject grid, string name, int sortingOrder, Color tint)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid.transform, false);

        var tilemap = go.GetComponent<Tilemap>();
        tilemap.color = tint;
        go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return tilemap;
    }

    private static GameObject Spawn(GameObject prefab, Scene scene, Transform parent, Vector2 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (parent != null) instance.transform.SetParent(parent, true);
        instance.transform.position = position;
        return instance;
    }

    private static Camera CreateCamera(Color background, float orthographicSize)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);

        var camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = orthographicSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
        go.AddComponent<AudioListener>();
        return camera;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ================================================================== UTILIDADES

    private static int Mask(int layer) => 1 << layer;

    /// <summary>
    /// Asigna campos [SerializeField] privados vía SerializedObject (igual que el Inspector).
    /// </summary>
    private static void Set(Object target, params (string name, object value)[] values)
    {
        var serialized = new SerializedObject(target);

        foreach ((string name, object value) in values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name}: no existe el campo '{name}'.");
                continue;
            }

            switch (value)
            {
                case float f: property.floatValue = f; break;
                case int i when property.propertyType == SerializedPropertyType.Enum: property.enumValueIndex = i; break;
                case int i: property.intValue = i; break;
                case bool b: property.boolValue = b; break;
                case string s: property.stringValue = s; break;
                case Color color: property.colorValue = color; break;
                case Vector2 vector: property.vector2Value = vector; break;
                case Object reference: property.objectReferenceValue = reference; break;
                case Array array:
                    property.arraySize = array.Length;
                    for (int k = 0; k < array.Length; k++)
                    {
                        SerializedProperty element = property.GetArrayElementAtIndex(k);
                        object item = array.GetValue(k);
                        if (item is string text) element.stringValue = text;
                        else element.objectReferenceValue = item as Object;
                    }
                    break;
                default:
                    Debug.LogError($"{target.GetType().Name}.{name}: tipo no soportado {value?.GetType().Name}.");
                    break;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
