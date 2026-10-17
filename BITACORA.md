# Unfilial — Game Developer's Journal (Unidad 2D)

> Borrador generado durante el desarrollo. Revísalo y complétalo con tus propias palabras antes de entregarlo.

## 1. Licencias y procedencia de assets

| Asset | Origen | Licencia |
|---|---|---|
| Sprites del personaje, enemigos, orbes, portal, escudo y tajo | Generados por código (`UnfilialBuilder.cs`, pixel art procedural) | Propios del proyecto |
| Tileset (16 tiles de suelo con auto-tiling, 1 agrietado, pinchos, 2 de fondo) | Generado por código | Propios del proyecto |
| Música y efectos de sonido (`_Project/Audio`) | Sintetizados por código (`ProceduralAudio.cs`) y exportados a `.wav` | Propios del proyecto |
| Fuente LiberationSans SDF | TextMesh Pro Essential Resources (Unity) | SIL Open Font License 1.1 |
| Sprite `Capsule` (ya no se usa en el jugador) | Paquete `com.unity.2d.sprite` | Unity Companion License |

No se usan assets de terceros (Kenney, itch.io, etc.).

## 2. Estructura del proyecto

```
Assets/_Project/
├── Animations/Player     (6 clips + Player.controller)
├── Art/Sprites           (hojas Multiple, PPU 32, Point, sin compresión)
├── Art/Tiles · Art/Palettes (Unfilial_Palette) · Art/Fonts
├── Audio/Music · Audio/SFX · Audio/MainMixer.mixer
├── Levels                (mapas .txt de los 3 niveles)
├── Prefabs · Scenes · Scripts · VFX/Prefabs
```

## 3. Arquitectura (SOLID)

- **Singleton:** `GameManager` (orbes y nivel de poder, `DontDestroyOnLoad`), `AudioManager` (persistente) y `UIManager.Instance` (de escena).
- **Observer:** `GameEvents` centraliza la comunicación (`OnOrbsChanged`, `OnHealthChanged`, `OnPlayerDeath`, `OnLevelCompleted`, `OnGamePaused`, `OnPowerLevelChanged`, `OnPlaySfx`, `OnVolumeChanged`). Todas las suscripciones son `+=` en `OnEnable` y `-=` en `OnDisable`.
- **SRP en el jugador:** `PlayerInput`, `PlayerMovement`, `PlayerHealth`, `PlayerAttack`, `PlayerShield`, `PlayerAudio`, `PlayerAnimator` y `PlayerCameraShake`. Los módulos se comunican con eventos C# locales (`Jumped`, `Attacked`, `Damaged`, `Blocked`...).
- **Object Pool:** `ProjectilePool` (`UnityEngine.Pool.ObjectPool<Projectile>`) recicla los proyectiles del enemigo Vigía con `Get()` / `Release()`.

## 4. Reporte de refactorización

1. **Script monolítico → módulos SRP:** el sonido salía de cada módulo (`PlayerAttack`, `PlayerHealth`...). Se movió a `PlayerAudio`, que escucha eventos locales; lo mismo con la animación (`PlayerAnimator`) y el screen shake (`PlayerCameraShake`).
2. **Daño acoplado → interfaz:** `PlayerAttack` daña cualquier `IDamageable`, sin conocer los tipos de enemigo.
3. **Trampa de muerte instantánea → peligro justo:** `DamageZone` pasó de 100 de daño al entrar a 20 de daño con rebote hacia arriba e i-frames, y se colocó en un `Hazards_Tilemap`.
4. **Escenas hechas a mano → generación por datos:** los niveles se describen en `.txt` y `UnfilialBuilder` genera escenas, prefabs y assets de forma reproducible.
5. **Audio en runtime → assets importados:** los clips pasaron de sintetizarse al arrancar a ser `.wav` con ajustes de importación (SFX con *Decompress On Load*, música con *Streaming* + *Vorbis*) y a salir por un `AudioMixer`.

## 5. Game feel

Gravedad asimétrica al caer, doble salto, dash, pogo al atacar hacia abajo, i-frames con parpadeo, flash rojo de daño (`unscaledDeltaTime`), screen shake con Cinemachine Impulse, hit-flash y knockback en enemigos, partículas `VFX_HitImpact` y variación de tono en los SFX.
