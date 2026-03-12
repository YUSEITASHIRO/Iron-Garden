using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;
using System.Runtime.InteropServices.JavaScript;

// ═══════════════════════════════════════════════════════════════════════════════
// IRON GARDEN  —  OVERHEAD 3D BUILDER EDITION
// Center Plant, Random Edge Source, 4 Enemy Factions, UI Icon Selection
// ═══════════════════════════════════════════════════════════════════════════════
public partial class Program
{
    const int SCREEN_W = 1280;
    const int SCREEN_H = 720;
    const int MAP_W = 72;
    const int MAP_H = 32;
    const int FPS = 60;
    const int MAX_DAYS = 7;

    enum GameState { Title, Day, Night, GameOver, Victory }
    static GameState state = GameState.Title;
    static int currentDay = 1;
    static Random rng = new Random();

    // ─── Camera ───────────────────────────────────────────────────────────────
    static Camera3D camera;
    static Vector3 camTarget = new Vector3(MAP_W / 2f, 0, MAP_H / 2f);
    static float camZoom = 28f;
    static float camAngle = 0f;

    // ─── 3D World ─────────────────────────────────────────────────────────────
    enum Block { Air, Floor, Source, Plant, River, Channel, Wall, Turret, Mine, Decoy }
    static Block[,] map = new Block[MAP_W, MAP_H];
    static int[,] blockHp = new int[MAP_W, MAP_H];
    static bool[,] isWatered = new bool[MAP_W, MAP_H];
    
    // ─── Stats & Resources ────────────────────────────────────────────────────
    static int scrap = 350; 
    static int hpPlant = 100, hpPlantMax = 100;
    static float sanity = 100f, sanityMax = 100f;
    
    // Block Costs & Max HP
    static int costChannel = 2; static int maxHpChannel = 15;
    static int costWall = 4;    static int maxHpWall = 80;
    static int costTurret = 60; static int maxHpTurret = 40;
    static int costMine = 6;    static int maxHpMine = 5;
    static int costDecoy = 15;  static int maxHpDecoy = 150;

    static Block[] buildableBlocks = { Block.Channel, Block.Wall, Block.Turret, Block.Mine, Block.Decoy };
    static int buildIdx = 0;
    static int placementMode = (int)Block.Channel; 

    static int sourceX, sourceZ;
    static int plantX = MAP_W / 2, plantZ = MAP_H / 2;

    // ─── Combat Entities ──────────────────────────────────────────────────────
    enum Faction { Predator, Saboteur, Crusher, Cultist }
    class Enemy
    {
        public Vector3 pos;
        public int hp, hpMax, dmg;
        public float speed;
        public Faction faction;
        public bool alive = true;
        public float atkTimer = 0f;
        public Vector3 targetBlock;
    }
    static List<Enemy> enemies = new List<Enemy>();
    
    struct TurretState { public int x, z; public float cooldown; }
    static List<TurretState> turrets = new List<TurretState>();

    struct Bullet { public Vector3 pos, vel; public int dmg; public bool alive; }
    static List<Bullet> bullets = new List<Bullet>();
    
    struct Particle { public Vector3 pos, vel; public Color col; public float life; public float size; }
    static List<Particle> particles = new List<Particle>();

    // ─── Wave Management ──────────────────────────────────────────────────────
    static int enemiesToSpawn = 0;
    static float spawnTimer = 0f;
    static float nightTimer = 0f;

    // ─── UI & Interaction ─────────────────────────────────────────────────────
    static RaycastHit lookAtHit;
    static bool hasLookAt = false;
    static float horrorTimer = 0f;

    // Colors
    static Color UI_BG = new Color(15, 18, 22, 240);
    static Color UI_OUTLINE = new Color(50, 60, 70, 255);
    static Color TEXT_MAIN = new Color(230, 240, 250, 255);
    
    static Color C_FLOOR = new Color(25, 30, 35, 255);
    static Color C_SOURCE = new Color(0, 150, 255, 255);
    static Color C_PLANT = new Color(50, 220, 80, 255);
    static Color C_RIVER = new Color(20, 120, 200, 255);
    static Color C_CHANNEL_DRY = new Color(100, 110, 120, 255);
    static Color C_CHANNEL_WET = new Color(0, 200, 255, 255);
    static Color C_WALL = new Color(140, 100, 50, 255);
    static Color C_TURRET = new Color(180, 190, 200, 255);
    static Color C_MINE = new Color(220, 40, 40, 255);
    static Color C_DECOY = new Color(255, 180, 0, 255);
    static Color C_SCRAP = new Color(255, 200, 50, 255);

    // ═══════════════════════════════════════════════════════════════════════════
    public static void Main()
    {
        InitWindow(SCREEN_W, SCREEN_H, "IRON GARDEN");
        SetTargetFPS(FPS);
        InitAudioDevice();

        camera = new Camera3D();
        camera.Position = new Vector3(camTarget.X, camZoom, camTarget.Z + camZoom * 0.8f);
        camera.Target = camTarget;
        camera.Up = new Vector3(0, 1, 0);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;
        
        StartNewGame();

        if (!OperatingSystem.IsBrowser())
        {
            while (!WindowShouldClose()) { Update(); }
            CloseAudioDevice();
            CloseWindow();
        }
    }

    [JSExport]
    public static void Update()
    {
        float dt = GetFrameTime();
        horrorTimer += dt;
        
        switch (state)
        {
            case GameState.Title: UpdateTitle(dt); break;
            case GameState.Day: UpdateDay(dt); break;
            case GameState.Night: UpdateNight(dt); break;
            case GameState.GameOver: if (IsKeyPressed(KeyboardKey.Enter)) StartNewGame(); break;
            case GameState.Victory: if (IsKeyPressed(KeyboardKey.Enter)) StartNewGame(); break;
        }

        UpdateParticles(dt);

        BeginDrawing();
        ClearBackground(new Color(5, 7, 10, 255));

        if (state != GameState.Title && state != GameState.GameOver && state != GameState.Victory)
        {
            float sway = sanity < 40f ? MathF.Sin(horrorTimer * 10f) * (40f - sanity) * 0.05f : 0f;
            camera.FovY = 45.0f + sway;

            BeginMode3D(camera);
            DrawWorld3D();
            EndMode3D();
            DrawHUD();
            if (sanity < 50f) DrawGlitchEffect();
        }
        else
        {
            if (state == GameState.Title) DrawTitle();
            if (state == GameState.GameOver) DrawScreenWrap("PLANT OR SANITY LOST", new Color(220,50,50,255));
            if (state == GameState.Victory) DrawScreenWrap("7 NIGHTS SURVIVED. YOU HAVE SAVED IT.", C_PLANT);
        }

        EndDrawing();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    static void StartNewGame()
    {
        scrap = 350; hpPlant = hpPlantMax = 100; sanity = sanityMax = 100f;
        currentDay = 1; state = GameState.Title;
        GenerateMap();
        camTarget = new Vector3(MAP_W / 2f, 0, MAP_H / 2f);
        UpdateCamera(0);
    }

    static void GenerateMap()
    {
        turrets.Clear(); enemies.Clear(); bullets.Clear(); particles.Clear();
        for (int x = 0; x < MAP_W; x++) {
            for (int z = 0; z < MAP_H; z++) {
                map[x, z] = Block.Air;
                isWatered[x,z] = false;
                blockHp[x,z] = 0;
            }
        }

        // Field A (Plant Area)
        for(int x = 2; x <= 33; x++) for(int z = 2; z <= 29; z++) map[x, z] = Block.Floor;
        // Field B (Source Area)
        for(int x = 38; x <= 69; x++) for(int z = 2; z <= 29; z++) map[x, z] = Block.Floor;
        
        // Bridge connecting them
        for(int x = 34; x <= 37; x++) for(int z = 14; z <= 17; z++) map[x, z] = Block.Floor;

        // Center Plant in Field A
        plantX = 17; plantZ = 16;
        map[plantX, plantZ] = Block.Plant;
        
        // Center Source in Field B
        sourceX = 54; sourceZ = 16;
        map[sourceX, sourceZ] = Block.Source;

        // Generate Natural River flowing towards bridge
        int rx = sourceX; int rz = sourceZ;
        int maxFlow = rng.Next(15, 25);
        for(int i=0; i<maxFlow; i++) {
            if (rng.Next(2) == 0) {
                if (rx > 35) rx--; // Flow mostly left towards bridge
            } else {
                if (rz < 16) rz++; else if (rz > 16) rz--; 
            }
            if (rx <= 35) break; // Don't cross bridge 
            if (map[rx,rz] == Block.Floor) map[rx,rz] = Block.River;
        }

        // Random unbuildable debris / holes
        for (int i=0; i<60; i++) {
            int dx = rng.Next(2, MAP_W-2); int dz = rng.Next(2, MAP_H-2);
            if (map[dx,dz] == Block.Floor && Vector2.Distance(new Vector2(dx,dz), new Vector2(plantX,plantZ)) > 3 && Vector2.Distance(new Vector2(dx,dz), new Vector2(sourceX,sourceZ)) > 3) 
                map[dx,dz] = Block.Air; 
        }
        RecalculateWater();
    }

    static void RecalculateWater()
    {
        for (int x=0; x<MAP_W; x++) for(int z=0; z<MAP_H; z++) isWatered[x,z] = false;
        Queue<(int x, int z)> q = new Queue<(int, int)>();
        q.Enqueue((sourceX, sourceZ));
        isWatered[sourceX, sourceZ] = true;

        int[] dx = {1, -1, 0, 0};
        int[] dz = {0, 0, 1, -1};

        while(q.Count > 0)
        {
            var curr = q.Dequeue();
            for(int i=0; i<4; i++) {
                int nx = curr.x + dx[i]; int nz = curr.z + dz[i];
                if (nx>=0 && nx<MAP_W && nz>=0 && nz<MAP_H && !isWatered[nx,nz]) {
                    if (map[nx,nz] == Block.Channel || map[nx,nz] == Block.Plant || map[nx,nz] == Block.Source || map[nx,nz] == Block.River) {
                        isWatered[nx,nz] = true;
                        q.Enqueue((nx,nz));
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    static void UpdateCamera(float dt)
    {
        Vector3 forward = new Vector3(-MathF.Sin(camAngle), 0, -MathF.Cos(camAngle));
        Vector3 right = new Vector3(MathF.Cos(camAngle), 0, -MathF.Sin(camAngle));

        float speed = 30f * dt;
        if (IsKeyDown(KeyboardKey.W)) camTarget += forward * speed;
        if (IsKeyDown(KeyboardKey.S)) camTarget -= forward * speed;
        if (IsKeyDown(KeyboardKey.A)) camTarget -= right * speed;
        if (IsKeyDown(KeyboardKey.D)) camTarget += right * speed;

        float wheel = GetMouseWheelMove();
        if (wheel != 0) camZoom = Math.Clamp(camZoom - wheel * 3f, 10f, 40f);

        if (IsMouseButtonDown(MouseButton.Middle)) {
            Vector2 delta = GetMouseDelta();
            if (IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift)) {
                camTarget -= right * (delta.X * 0.02f * camZoom / 15f);
                camTarget += forward * (delta.Y * 0.02f * camZoom / 15f);
            } else {
                camAngle += delta.X * 0.01f;
            }
        }

        camTarget.X = Math.Clamp(camTarget.X, 0, MAP_W);
        camTarget.Z = Math.Clamp(camTarget.Z, 0, MAP_H);

        camera.Target = camTarget;
        float dist = camZoom * 0.8f;
        camera.Position = new Vector3(camTarget.X + MathF.Sin(camAngle)*dist, camZoom, camTarget.Z + MathF.Cos(camAngle)*dist);
        DoRaycast();
    }

    struct RaycastHit { public int x, z; public Block block; public bool hit; }
    static void DoRaycast()
    {
        hasLookAt = false; lookAtHit.hit = false;
        Ray ray = GetScreenToWorldRay(GetMousePosition(), camera);
        
        if (ray.Direction.Y < 0) {
            float t = -ray.Position.Y / ray.Direction.Y;
            Vector3 p = ray.Position + ray.Direction * t;
            int hx = (int)Math.Round(p.X); int hz = (int)Math.Round(p.Z);
            
            if (hx >= 0 && hx < MAP_W && hz >= 0 && hz < MAP_H) {
                hasLookAt = true;
                lookAtHit = new RaycastHit { x=hx, z=hz, block=map[hx,hz], hit=true };
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    static void UpdateTitle(float dt) {
        UpdateCamera(dt);
        camTarget.Z = MAP_H/2f; camTarget.X = MathF.Sin(horrorTimer)*5 + MAP_W/2f;
        if (IsKeyPressed(KeyboardKey.Enter) || IsMouseButtonPressed(MouseButton.Left)) state = GameState.Day;
    }

    static void HandleBuilding()
    {
        Vector2 mp = GetMousePosition();
        
        // Block picking via keyboard as fallback
        if (IsKeyPressed(KeyboardKey.One)) { buildIdx=0; placementMode=(int)Block.Channel; }
        if (IsKeyPressed(KeyboardKey.Two)) { buildIdx=1; placementMode=(int)Block.Wall; }
        if (IsKeyPressed(KeyboardKey.Three)){buildIdx=2; placementMode=(int)Block.Turret; }
        if (IsKeyPressed(KeyboardKey.Four)) {buildIdx=3; placementMode=(int)Block.Mine; }
        if (IsKeyPressed(KeyboardKey.Five)) {buildIdx=4; placementMode=(int)Block.Decoy; }

        // Horizontal UI Click Selection
        if (mp.Y >= SCREEN_H - 100) {
            if (IsMouseButtonPressed(MouseButton.Left)) {
                int startX = SCREEN_W / 2 - (buildableBlocks.Length * 110) / 2;
                for (int i = 0; i < buildableBlocks.Length; i++) {
                    Rectangle btnRec = new Rectangle(startX + i * 110, SCREEN_H - 85, 100, 70);
                    if (CheckCollisionPointRec(mp, btnRec)) {
                        buildIdx = i;
                        placementMode = (int)buildableBlocks[buildIdx];
                    }
                }
            }
            return; // Interacting with UI, don't build
        }

        if (hasLookAt && lookAtHit.block != Block.Air && lookAtHit.block != Block.Source && lookAtHit.block != Block.Plant)
        {
            if (IsMouseButtonDown(MouseButton.Left) && (lookAtHit.block == Block.Floor || lookAtHit.block == Block.River))
            {
                int cost = GetCost((Block)placementMode);
                if (scrap >= cost) {
                    scrap -= cost;
                    map[lookAtHit.x, lookAtHit.z] = (Block)placementMode;
                    blockHp[lookAtHit.x, lookAtHit.z] = GetMaxHp((Block)placementMode);
                    if (placementMode == (int)Block.Turret) turrets.Add(new TurretState { x=lookAtHit.x, z=lookAtHit.z, cooldown=0 });
                    RecalculateWater();
                    CreateParticles(new Vector3(lookAtHit.x, 0, lookAtHit.z), Color.White, 5);
                }
            }
            else if (IsMouseButtonDown(MouseButton.Right) && lookAtHit.block != Block.Floor && lookAtHit.block != Block.River)
            {
                Block b = map[lookAtHit.x, lookAtHit.z];
                scrap += GetCost(b); // Full refund
                map[lookAtHit.x, lookAtHit.z] = Block.Floor;
                blockHp[lookAtHit.x, lookAtHit.z] = 0;
                if (b == Block.Turret) turrets.RemoveAll(t => t.x==lookAtHit.x && t.z==lookAtHit.z);
                RecalculateWater();
                CreateParticles(new Vector3(lookAtHit.x, 0, lookAtHit.z), Color.Gray, 5);
            }
        }
    }

    static void UpdateDay(float dt)
    {
        UpdateCamera(dt);
        HandleBuilding();

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.Space)) {
            state = GameState.Night;
            nightTimer = 40f + (currentDay * 8f);
            enemiesToSpawn = 10 + currentDay * 10;
            spawnTimer = 1f;
            bullets.Clear();
        }
    }

    static int GetCost(Block b) => b switch { Block.Channel=>costChannel, Block.Wall=>costWall, Block.Turret=>costTurret, Block.Mine=>costMine, Block.Decoy=>costDecoy, _=>0 };
    static int GetMaxHp(Block b) => b switch { Block.Channel=>maxHpChannel, Block.Wall=>maxHpWall, Block.Turret=>maxHpTurret, Block.Decoy=>maxHpDecoy, _=>0 };
    static Color GetColor(Block b) => b switch { Block.Channel=>C_CHANNEL_WET, Block.Wall=>C_WALL, Block.Turret=>C_TURRET, Block.Mine=>C_MINE, Block.Decoy=>C_DECOY, _=>Color.White };
    static string GetName(Block b) => b switch { Block.Channel=>"Water Channel", Block.Wall=>"Barricade Wall", Block.Turret=>"Auto Turret", Block.Mine=>"Explosive Mine", Block.Decoy=>"Decoy Beacon", _=>"" };

    static float hpAcc = 0f;

    static void UpdateNight(float dt)
    {
        UpdateCamera(dt);
        HandleBuilding(); 
        
        nightTimer -= dt;

        if (isWatered[plantX, plantZ]) {
            sanity = Math.Min(sanityMax, sanity + 5f * dt);
            hpAcc += 5f * dt;
            if (hpAcc >= 1f) { hpPlant = Math.Min(hpPlantMax, hpPlant + (int)hpAcc); hpAcc -= (int)hpAcc; }
        } else {
            sanity -= 2.5f * dt;
            hpAcc -= 2f * dt;
            if (hpAcc <= -1f) { hpPlant += (int)hpAcc; hpAcc -= (int)hpAcc; }
        }

        spawnTimer -= dt;
        if (spawnTimer <= 0 && enemiesToSpawn > 0) {
            enemiesToSpawn--; spawnTimer = 1.5f - (currentDay * 0.1f);
            SpawnEnemy();
        }

        // Turret Fire
        for(int i=0; i<turrets.Count; i++) {
            var t = turrets[i]; t.cooldown -= dt;
            if(t.cooldown <= 0) {
                Enemy eTarget = null; float minDist = 9f;
                foreach(var e in enemies) if(e.alive && Vector3.Distance(e.pos, new Vector3(t.x,0,t.z)) < minDist) { minDist = Vector3.Distance(e.pos, new Vector3(t.x,0,t.z)); eTarget = e; }
                if(eTarget != null) {
                    Vector3 d = Vector3.Normalize(eTarget.pos - new Vector3(t.x, 0.5f, t.z));
                    bullets.Add(new Bullet { pos=new Vector3(t.x, 0.5f, t.z), vel=d*20f, dmg=15, alive=true });
                    t.cooldown = 0.8f;
                }
            }
            turrets[i] = t;
        }

        // Bullets
        for(int i=0; i<bullets.Count; i++) {
            var b = bullets[i]; if(!b.alive) continue;
            b.pos += b.vel * dt;
            CreateParticles(b.pos, Color.Yellow, 1, 0.05f);

            foreach(var e in enemies) {
                if(e.alive && Vector3.Distance(b.pos, e.pos) < 0.6f) {
                    e.hp -= b.dmg;
                    if(e.hp <= 0) { e.alive = false; scrap += 15 + (currentDay * 3); CreateParticles(e.pos, Color.Red, 15); }
                    b.alive = false; break;
                }
            }
            int bx=(int)Math.Round(b.pos.X), bz=(int)Math.Round(b.pos.Z);
            if(bx>=0 && bx<MAP_W && bz>=0 && bz<MAP_H && (map[bx,bz]==Block.Wall || map[bx,bz]==Block.Decoy)) b.alive=false;
            bullets[i]=b;
        }
        bullets.RemoveAll(x => !x.alive);

        // Enemies AI & Movement
        for(int i=0; i<enemies.Count; i++) {
            var e = enemies[i]; if(!e.alive) continue;

            // Mines Trigger
            int cx = (int)Math.Round(e.pos.X); int cz = (int)Math.Round(e.pos.Z);
            if (cx>=0 && cx<MAP_W && cz>=0 && cz<MAP_H && map[cx,cz] == Block.Mine) {
                map[cx,cz] = Block.Floor;
                CreateParticles(new Vector3(cx,0,cz), Color.Orange, 40, 0.5f);
                foreach(var en in enemies) {
                    if(en.alive && Vector3.Distance(en.pos, new Vector3(cx,0,cz)) < 3.0f) { 
                        en.hp -= 80; en.speed *= 0.6f; 
                        if(en.hp <= 0) { en.alive = false; scrap += 15 + (currentDay * 3); CreateParticles(en.pos, Color.Red, 15); }
                    }
                }
                if (e.hp <= 0) { e.alive=false; continue; }
            }

            // Target Priority
            if (e.faction == Faction.Saboteur) {
                // Seek nearest channel or river to sever water
                Vector3 bt = new Vector3(plantX, 0, plantZ); float dist = float.MaxValue;
                for(int x=0;x<MAP_W;x++) for(int z=0;z<MAP_H;z++) if(map[x,z] == Block.Channel || map[x,z] == Block.River) {
                    float d = Vector2.Distance(new Vector2(e.pos.X, e.pos.Z), new Vector2(x,z));
                    if(d<dist) { dist=d; bt = new Vector3(x,0,z); }
                }
                e.targetBlock = bt;
            } else if (e.faction == Faction.Crusher) {
                // Seek nearest Turret
                Vector3 bt = new Vector3(plantX, 0, plantZ); float dist = float.MaxValue;
                for(int x=0;x<MAP_W;x++) for(int z=0;z<MAP_H;z++) if(map[x,z] == Block.Turret) {
                    float d = Vector2.Distance(new Vector2(e.pos.X, e.pos.Z), new Vector2(x,z));
                    if(d<dist) { dist=d; bt = new Vector3(x,0,z); }
                }
                e.targetBlock = bt;
            } else { // Predator & Bomber
                Vector3 bt = new Vector3(plantX, 0, plantZ); float dist = Vector2.Distance(new Vector2(e.pos.X, e.pos.Z), new Vector2(plantX, plantZ));
                // Decoys constantly attract them
                for(int x=0;x<MAP_W;x++) for(int z=0;z<MAP_H;z++) if(map[x,z] == Block.Decoy) {
                    float d = Vector2.Distance(new Vector2(e.pos.X, e.pos.Z), new Vector2(x,z));
                    if(d<dist) { dist=d; bt = new Vector3(x,0,z); }
                }
                e.targetBlock = bt;
            }

            Vector3 dir = Vector3.Normalize(e.targetBlock - e.pos); dir.Y = 0;
            float distToTarget = Vector2.Distance(new Vector2(e.pos.X, e.pos.Z), new Vector2(e.targetBlock.X, e.targetBlock.Z));
            
            if (distToTarget > 0.6f) {
                Vector3 np = e.pos + dir * e.speed * dt;
                int ix = (int)Math.Round(np.X), iz = (int)Math.Round(np.Z);
                
                if (ix>=0 && ix<MAP_W && iz>=0 && iz<MAP_H && 
                    (map[ix,iz]==Block.Wall || map[ix,iz]==Block.Turret || map[ix,iz]==Block.Decoy || map[ix,iz]==Block.Channel)) {
                    
                    if (e.faction == Faction.Cultist) {
                        ExplodeCultist(e);
                        continue;
                    } else {
                        e.atkTimer -= dt;
                        if(e.atkTimer <= 0) {
                            int dealDmg = e.dmg > 0 ? e.dmg : 15; 
                            blockHp[ix,iz] -= dealDmg;
                            CreateParticles(np, Color.Gray, 5);
                            if (blockHp[ix,iz] <= 0) {
                                if (map[ix,iz] == Block.Turret) turrets.RemoveAll(t => t.x==ix && t.z==iz);
                                map[ix,iz] = Block.Floor;
                                RecalculateWater();
                            }
                            e.atkTimer = 1.0f;
                        }
                    }
                } else { e.pos = np; }
            } else {
                if (e.faction == Faction.Cultist) {
                    ExplodeCultist(e); continue;
                } else {
                    e.atkTimer -= dt;
                    if(e.atkTimer<=0) {
                        int tx = (int)e.targetBlock.X; int tz = (int)e.targetBlock.Z;
                        if (tx == plantX && tz == plantZ) {
                            hpPlant -= Math.Max(1, e.dmg / 2); // 直接のコアダメージを半減
                        } else {
                            if (map[tx,tz] != Block.Floor && map[tx,tz] != Block.Air && map[tx,tz] != Block.Source) {
                                int dealDmg = e.dmg > 0 ? e.dmg : 15;
                                blockHp[tx,tz] -= dealDmg;
                                CreateParticles(e.targetBlock, Color.Gray, 5);
                                if (blockHp[tx,tz] <= 0) {
                                    if (map[tx,tz] == Block.Turret) turrets.RemoveAll(t => t.x==tx && t.z==tz);
                                    map[tx,tz] = Block.Floor;
                                    RecalculateWater();
                                }
                            }
                        }

                        if(e.faction == Faction.Saboteur) {
                            if (map[tx,tz] == Block.River || map[tx,tz] == Block.Channel) { map[tx,tz] = Block.Floor; RecalculateWater(); }
                        }
                        e.atkTimer = 1.2f;
                    }
                }
            }
            enemies[i] = e;
        }
        enemies.RemoveAll(x => !x.alive);

        if (hpPlant <= 0 || sanity <= 0) { state = GameState.GameOver; }
        else if (nightTimer <= 0) { 
            currentDay++;
            if (currentDay > MAX_DAYS) { state = GameState.Victory; }
            else {
                scrap += 150 + (currentDay * 40) + (currentDay * currentDay * 10); // Quadratic Night Survival Bonus (Balanced)
                sanity = Math.Min(sanityMax, sanity + 40f);
                enemies.Clear();
                bullets.Clear();
                
                // ターン経過による建造物の風化（最大HPの50%ダメージ）
                for (int x = 0; x < MAP_W; x++) {
                    for (int z = 0; z < MAP_H; z++) {
                        if (map[x,z] == Block.Wall || map[x,z] == Block.Turret || map[x,z] == Block.Channel || map[x,z] == Block.Decoy) {
                            blockHp[x,z] -= GetMaxHp(map[x,z]) / 2;
                            if (blockHp[x,z] <= 0) {
                                if (map[x,z] == Block.Turret) turrets.RemoveAll(t => t.x==x && t.z==z);
                                map[x,z] = Block.Floor;
                            }
                        }
                    }
                }
                RecalculateWater();

                state = GameState.Day;
            }
        }
    }

    static void ExplodeCultist(Enemy e) {
        CreateParticles(e.pos, new Color(50, 200, 50, 255), 50, 0.4f); // 緑色の狂信爆発
        int cx = (int)Math.Round(e.pos.X); int cz = (int)Math.Round(e.pos.Z);
        for(int x=-1; x<=1; x++) for(int z=-1; z<=1; z++) {
            int ix = cx+x, iz = cz+z;
            if (ix>=0 && ix<MAP_W && iz>=0 && iz<MAP_H) {
                if (map[ix,iz] == Block.Plant) { hpPlant -= 5; sanity -= 10f; } // コアダメージ＋大幅な精神汚染
                else if (map[ix,iz] != Block.Source && map[ix,iz] != Block.Floor && map[ix,iz] != Block.Air && map[ix,iz] != Block.River) {
                    if (map[ix,iz] == Block.Turret) turrets.RemoveAll(t => t.x==ix && t.z==iz);
                    map[ix,iz] = Block.Floor; blockHp[ix,iz] = 0;
                }
            }
        }
        e.alive = false;
        RecalculateWater();
    }

    static void SpawnEnemy()
    {
        Faction f = (Faction)rng.Next(0, 4);
        int sx = 0, sz = 0;
        int edge = rng.Next(6);
        if (edge == 0) { sx = rng.Next(2, 34); sz = 0; } // Top A
        else if (edge == 1) { sx = rng.Next(38, 70); sz = 0; } // Top B
        else if (edge == 2) { sx = rng.Next(2, 34); sz = 31; } // Bottom A
        else if (edge == 3) { sx = rng.Next(38, 70); sz = 31; } // Bottom B
        else if (edge == 4) { sx = 0; sz = rng.Next(2, 30); } // Left
        else { sx = 71; sz = rng.Next(2, 30); } // Right

        int hp = f switch { Faction.Predator=>40, Faction.Saboteur=>25, Faction.Crusher=>60, Faction.Cultist=>30, _=>30 } + (currentDay * 12) + (currentDay * currentDay * 4);
        float spd = f switch { Faction.Predator=>2.5f, Faction.Saboteur=>3.2f, Faction.Crusher=>1.8f, Faction.Cultist=>3.5f, _=>3f };
        int dmg = f switch { Faction.Predator=>15, Faction.Saboteur=>10, Faction.Crusher=>25, Faction.Cultist=>0, _=>10 };

        enemies.Add(new Enemy { pos = new Vector3(sx, 0.5f, sz), hp=hp, hpMax=hp, dmg=dmg, speed=spd, faction=f });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    static void DrawWorld3D()
    {
        for (int x = 0; x < MAP_W; x++) {
            for(int z = 0; z < MAP_H; z++) {
                if (map[x,z] == Block.Air) continue;

                if (map[x,z] == Block.Floor || map[x,z] == Block.River) {
                    Color fc = map[x,z] == Block.River ? (isWatered[x,z] ? C_CHANNEL_WET : C_RIVER) : C_FLOOR;
                    float fh = map[x,z] == Block.River ? -0.1f : -0.05f;
                    DrawCube(new Vector3(x, fh, z), 0.9f, 0.1f, 0.9f, fc);
                    
                    if (hasLookAt && lookAtHit.x == x && lookAtHit.z == z) {
                        DrawCubeWires(new Vector3(x, 0.5f, z), 0.95f, 0.95f, 0.95f, Color.White); 
                    }
                    continue;
                }

                Color c = Color.White; float height = 1.0f; float w = 0.9f;
                switch(map[x,z]) {
                    case Block.Source: c = C_SOURCE; height=1.5f; break;
                    case Block.Plant: c = C_PLANT; height=1.5f; break;
                    case Block.Channel: c = isWatered[x,z] ? C_CHANNEL_WET : C_CHANNEL_DRY; height=0.4f; break;
                    case Block.Wall: c = C_WALL; height=1.2f; break;
                    case Block.Turret: c = C_TURRET; height=1.8f; w=0.6f; break;
                    case Block.Mine: c = C_MINE; height=0.2f; w=0.5f; break;
                    case Block.Decoy: c = C_DECOY; height=1.3f; w=0.8f; break;
                }
                
                if (blockHp[x,z] > 0 && blockHp[x,z] < GetMaxHp(map[x,z])/2 && MathF.Sin(horrorTimer*20)>0) c = Color.Red;

                DrawCube(new Vector3(x, height/2f, z), w, height, w, c);
                DrawCubeWires(new Vector3(x, height/2f, z), w, height, w, Color.Black);
            }
        }

        if (isWatered[plantX, plantZ]) {
            DrawCube(new Vector3(plantX, 2.5f + MathF.Sin(horrorTimer*5)*0.2f, plantZ), 0.4f, 0.4f, 0.4f, C_SOURCE);
        }

        foreach(var e in enemies) {
            Color ec = e.faction switch{Faction.Predator=>Color.Red, Faction.Saboteur=>Color.Orange, Faction.Crusher=>Color.DarkGray, Faction.Cultist=>new Color(50, 200, 50, 255), _=>Color.White };
            float size = e.faction == Faction.Crusher ? 0.8f : 0.6f;
            DrawCube(e.pos, size, 0.8f, size, ec);
            DrawCubeWires(e.pos, size, 0.8f, size, Color.Black);
        }

        foreach(var p in particles) DrawCube(p.pos, p.size, p.size, p.size, p.col);
    }

    static void CreateParticles(Vector3 pos, Color c, int count, float size=0.1f) {
        for(int i=0; i<count; i++) particles.Add(new Particle{ pos=pos, vel=new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble(), (float)rng.NextDouble() - 0.5f)*10f, col=c, life=0.5f+ (float)rng.NextDouble(), size=size});
    }
    static void UpdateParticles(float dt) {
        for(int i=0; i<particles.Count; i++) {
            var p = particles[i]; p.life-=dt; p.pos+=p.vel*dt; p.vel.Y -= 15f*dt;
            particles[i]=p;
        }
        particles.RemoveAll(p=>p.life<=0);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    static string Scramble(string src, float limit) {
        if (sanity > limit) return src;
        char[] arr = src.ToCharArray();
        for(int i=0; i<arr.Length; i++) if(rng.NextDouble() < 0.3) arr[i] = "!@#$%^&*()_+"[rng.Next(12)];
        return new string(arr);
    }

    static void DrawHUD()
    {
        DrawRectangle(0, 0, SCREEN_W, 60, UI_BG);
        DrawRectangleLines(0, 0, SCREEN_W, 60, UI_OUTLINE);
        DrawText(Scramble($"DAY {currentDay} / {MAX_DAYS}", 40), 30, 18, 24, TEXT_MAIN);
        DrawText($"SCRAP: {scrap}", 250, 20, 20, C_SCRAP);
        
        Color cnCol = isWatered[plantX, plantZ] ? C_SOURCE : new Color(220,50,50,255);
        string cnText = isWatered[plantX, plantZ] ? "PLANT WATERED: OK" : "PLANT WATERED: NO (DYING)";
        DrawText(cnText, 450, 20, 20, cnCol);

        DrawBar(950, 10, 250, 15, "Plant HP", hpPlant, hpPlantMax, new Color(50,220,80,255));
        DrawBar(950, 35, 250, 15, Scramble("Sanity", 60), (int)sanity, (int)sanityMax, new Color(180,80,255,255));

        DrawRectangle(0, SCREEN_H - 100, SCREEN_W, 100, UI_BG);
        DrawRectangleLines(0, SCREEN_H - 100, SCREEN_W, 100, UI_OUTLINE);

        // Horizontal Icon UI Block Selection
        int startX = SCREEN_W / 2 - (buildableBlocks.Length * 110) / 2;
        for (int i = 0; i < buildableBlocks.Length; i++) {
            Block bSelect = buildableBlocks[i];
            Rectangle btnRec = new Rectangle(startX + i * 110, SCREEN_H - 85, 100, 70);
            
            if (placementMode == (int)bSelect) {
                DrawRectangleRec(btnRec, new Color(80, 80, 80, 255));
                DrawRectangleLinesEx(btnRec, 2f, Color.White);
            } else {
                DrawRectangleRec(btnRec, new Color(30, 30, 30, 255));
                DrawRectangleLinesEx(btnRec, 1f, new Color(100, 100, 100, 255));
            }
            
            DrawRectangle(startX + i * 110 + 30, SCREEN_H - 75, 40, 20, GetColor(bSelect));
            
            string shortName = GetName(bSelect).Split(' ')[1]; // "Water Channel" -> "Channel"
            DrawText(shortName, startX + i * 110 + 50 - MeasureText(shortName, 10)/2, SCREEN_H - 45, 10, Color.White);
            
            string costStr = GetCost(bSelect).ToString();
            DrawText(costStr, startX + i * 110 + 50 - MeasureText(costStr, 18)/2, SCREEN_H - 30, 18, C_SCRAP);
            
            DrawText($"[{i+1}]", startX + i * 110 + 5, SCREEN_H - 80, 10, Color.Gray);
        }

        if (state == GameState.Day) {
            DrawText("BUILD PHASE", 30, SCREEN_H - 65, 30, TEXT_MAIN);
            DrawText("[ENTER/SPACE] START NIGHT", 250, SCREEN_H - 60, 20, new Color(255,150,50,255));
            DrawText("LMB: Build   RMB: Sell", 980, SCREEN_H - 60, 20, TEXT_MAIN);
        } else if (state == GameState.Night) {
            DrawText("DEFEND THE PLANT", 30, SCREEN_H - 70, 30, new Color(250,50,50,255));
            DrawText($"Survive: {Math.Max(0, nightTimer):F1}s", 320, SCREEN_H - 60, 24, TEXT_MAIN);
            DrawText("LMB: Build   RMB: Sell", 980, SCREEN_H - 60, 20, TEXT_MAIN);
        }
    }

    static void DrawBar(int x, int y, int w, int h, string label, int val, int max, Color col)
    {
        DrawText(label, x - MeasureText(label, 14) - 10, y, 14, TEXT_MAIN);
        DrawRectangle(x, y, w, h, new Color(20,20,20,255));
        DrawRectangle(x, y, (int)(w * Math.Clamp((float)val/Math.Max(1,max), 0, 1)), h, col);
    }

    static void DrawGlitchEffect() {
        float intensity = 1.0f - (sanity / 100f);
        if (rng.NextDouble() < intensity * 0.4) DrawRectangle(0, rng.Next(SCREEN_H), SCREEN_W, rng.Next(2, 20), new Color(rng.Next(255),rng.Next(255),rng.Next(255), 50));
        if (rng.NextDouble() < intensity * 0.1) DrawText(Scramble("CORRUPTION", 0), rng.Next(SCREEN_W), rng.Next(SCREEN_H), 40, new Color(255,0,0,100));
        if (rng.NextDouble() < 0.05) DrawCircle(rng.Next(SCREEN_W), rng.Next(SCREEN_H), rng.Next(10,50), new Color(200,0,0,50));
    }

    static void DrawTitle() {
        DrawRectangle(0,0,SCREEN_W,SCREEN_H, new Color(5,10,15,200));
        DrawText("IRON GARDEN", 100, 200, 80, C_PLANT);
        DrawText("A Mechanical Sentinel's Choice", 105, 290, 30, C_SOURCE);
        DrawText("> Click or Press [ENTER] to Deploy <", 100, 500, 24, TEXT_MAIN);
    }
    
    static void DrawScreenWrap(string t, Color c) {
        DrawRectangle(0,0,SCREEN_W,SCREEN_H, new Color(0,0,0,220));
        DrawText(t, SCREEN_W/2 - MeasureText(t,50)/2, 300, 50, c);
        DrawText("Press [ENTER] to Restart", SCREEN_W/2 - MeasureText("Press [ENTER] to Restart",24)/2, 400, 24, TEXT_MAIN);
    }
}
