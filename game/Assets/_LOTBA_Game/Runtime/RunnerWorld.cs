using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    /// <summary>Pooled runner presentation and swept encounters. Scenery never decides run state.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class RunnerWorld : MonoBehaviour
    {
        public const float LaneSpacing = RoadStandard.LaneSpacing;
        [SerializeField] private Transform rider;
        [SerializeField] private Transform[] wheels;
        [SerializeField] private Transform[] pedals;
        [SerializeField] private RoadStreamer roadStream;
        [SerializeField] private Camera worldCamera;
        private float riderDistance;
        private float depthBendStart, depthBendStrength;
        private bool refreshDepthBend;
        private bool depthBendRequested;
        private float letterLaneX;
        [SerializeField] private GameObject gemPrefab;
        private SpriteRenderer gemFlash;
        private PickupFlashSettings activeFlash;
        private Material defaultFlashMaterial;
        private MaterialPropertyBlock gemFlashProperties;
        private Quaternion gemFlashRoll;
        private static readonly int FlashTopTint = Shader.PropertyToID("_TopTint");
        private static readonly int FlashBottomTint = Shader.PropertyToID("_BottomTint");
        private static readonly int FlashRadialGradient = Shader.PropertyToID("_RadialGradient");
        private float gemFlashStarted;
        [SerializeField] private Transform riderShadow;
        [Header("Word collection")]
        [SerializeField] private bool wordCollectionEnabled;
        [SerializeField] private string[] collectibleWords = { "HABLAR ES GANAR" };
        [SerializeField] private TMPro.TMP_FontAsset letterFont;
        [SerializeField] private Material letterOutlineMaterial;
        [SerializeField, Min(20f)] private float letterInterval = 65f;
        [SerializeField, Min(0f)] private float letterClearance = 12f;
        [SerializeField, Min(0f)] private float letterHoverHeight = .18f;
        [SerializeField, Min(.1f)] private float letterHoverCycle = 2.8f;
        [SerializeField, Range(0f, 15f)] private float letterTilt = 6f;
        private const float LetterPickupHeight = 1.1f;
        private float letterHoverTime;
        private readonly Queue<int> pendingLetters = new Queue<int>();
        private readonly System.Random letterRandom = new System.Random();
        private TMPro.TextMeshPro letterPickup;
        private SpriteRenderer letterBeam;
        private MaterialPropertyBlock letterBeamProperties;
        private readonly SpriteRenderer[] letterMotes = new SpriteRenderer[10];
        private float letterBeamTime;
        private TMPro.TMP_FontAsset LetterFont => letterFont != null ? letterFont : TMPro.TMP_Settings.defaultFontAsset;
        private string activeWord;
        private bool[] collectedLetters;
        private int activeLetter = -1;
        private int nextWord;
        private float nextLetterDistance;
        private float scheduledLetterStart, scheduledLetterEnd;
        private int scheduledLettersPlaced;
        public event Action<string, bool[], bool> LetterCollected;
        private readonly JumpArc jump = new JumpArc();
        private readonly List<TrackItem> items = new List<TrackItem>(256);
        private readonly List<Crossing> crossings = new List<Crossing>(16);
        private Quaternion[] wheelOrigins;
        private Vector3 cameraOrigin;
        private Quaternion cameraRotation;
        private Vector3 cameraVelocity;
        private float boostCameraBlend;
        private bool pickupEffectsEnabled = true;
        private const int NormalImpactScatterLimit = 5;
        private const int TurboImpactScatterLimit = 12;
        private readonly Transform[] scatteredGems = new Transform[TurboImpactScatterLimit];
        private readonly Vector3[] scatterTrajectories = new Vector3[TurboImpactScatterLimit];
        private readonly Vector3[] scatterSpins = new Vector3[TurboImpactScatterLimit];
        private readonly System.Random scatterRandom = new System.Random();
        private Vector3 gemScatterOrigin;
        private float gemScatterTime;
        private int scatteredGemCount;
        private TurboAuraController turboAura;
        private float referenceFieldOfView;
        private Renderer[] riderRenderers;
        private Material riderImpactMaterial;
        private Material[][] riderMaterials;
        private Material[][] riderFadeMaterials;
        private readonly Dictionary<Material, Material> riderFadeCopies = new Dictionary<Material, Material>();
        private Material[][] riderWhiteMaterials;
        private bool riderImpactWhite;
        private bool riderImpactTransparent;
        private ObstacleSet obstacleOverride;
        private readonly HashSet<ObstacleDefinition> registeredObstacles = new HashSet<ObstacleDefinition>();
        private GameSettings settings;
        private System.Random random;
        private const float FirstPatternDistance = 58f;
        private float nextPatternDistance;
        private float nextProfileObstacle;
        private float gemIntensity = 1f, obstacleIntensity = 1f;
        private int encounterSection = -1;
        private float lastPatternEnd;
        private float nextVehicleSafeDistance;
        private int nextDecisionGemSection;
        private float decisionGemSectionStart;
        private float laneTime;
        private float laneOrigin;
        private int visualLane = 1;
        private int safeLane = 1;
        private int previousVehicleLane = -1;
        private int wave;
        private float pedalAngle;
        private const float CourseRampStart = 72f;
        private const float CourseRampLength = 12f;
        private const float CourseBusStart = CourseRampStart + CourseRampLength;
        private const float CourseRoofGap = 14f;
        private const float CourseFallGravity = 34f;
        private ObstacleDefinition courseCar, courseBus, courseArticulatedBus;
        private Transform courseScenery;
        private float courseHeightOffset, courseJumpBase, courseFallSpeed, courseEnd;
        private float courseAdjacentEnd, courseLandingStart;
        private const float ElevatedInterval = 240f;
        private float nextElevatedDistance;
        private int vehiclePatternIndex;
        private readonly List<ElevatedEncounter> elevatedEncounters = new List<ElevatedEncounter>();
        private sealed class ElevatedEncounter
        {
            public Transform Ramp;
            public float Start, End;
            public readonly List<VehicleSurface> Surfaces = new List<VehicleSurface>(6);
        }
        private readonly struct VehicleSurface
        {
            public readonly TrackItem Vehicle;
            public readonly float Start, End, X, HalfWidth, Low, High;
            public VehicleSurface(float start, float end, float x, float halfWidth, float low, float high, TrackItem vehicle = null)
            { Start = start; End = end; X = x; HalfWidth = halfWidth; Low = low; High = high; Vehicle = vehicle; }
        }

#if UNITY_EDITOR
        private bool previewGems = true, previewObstacles = true, previewInvulnerable;
        public void ConfigureVehicleCourse(ObstacleDefinition car, ObstacleDefinition bus, ObstacleDefinition articulatedBus)
        {
            courseCar = car;
            courseBus = bus;
            courseArticulatedBus = articulatedBus;
            WarmObstacle(car);
            WarmObstacle(bus);
            WarmObstacle(articulatedBus);
        }

        private void SpawnVehicleCourse()
        {
            courseHeightOffset = courseJumpBase = courseFallSpeed = 0f;
            if (courseCar == null || courseBus == null || courseArticulatedBus == null) return;
            if (courseScenery != null) Destroy(courseScenery.gameObject);
            courseScenery = new GameObject("Vehicle test course").transform;
            courseScenery.SetParent(transform, false);
            float roof = courseBus.Height;
            float firstEnd = CourseBusStart + courseBus.Length;
            float adjacentStart = CourseBusStart + 2f;
            courseAdjacentEnd = adjacentStart + courseBus.Length;
            courseLandingStart = courseAdjacentEnd + CourseRoofGap;
            const float jointLength = 1.4f;
            float jointStart = courseLandingStart + courseArticulatedBus.Length;
            courseEnd = jointStart + jointLength + courseArticulatedBus.Length;
            Place(ItemKind.Obstacle, 1, 44f + courseCar.Length * .5f, 0f, courseCar);
            PlaceCourseBus(courseBus, 1, CourseBusStart);
            PlaceCourseBus(courseBus, 2, adjacentStart);
            PlaceCourseBus(courseArticulatedBus, 2, courseLandingStart);
            PlaceCourseBus(courseArticulatedBus, 2, jointStart + jointLength);
            CreateCourseSurface("Entry ramp", 0f, CourseRampStart, CourseBusStart, 0f, roof, 2f);
            CreateCourseSurface("Roof crossing", LaneSpacing * .5f, adjacentStart, firstEnd,
                roof, roof, LaneSpacing, roofBridge: true);
            CreateCourseSurface("Articulated joint", LaneSpacing, jointStart, jointStart + jointLength,
                roof, roof, courseArticulatedBus.Width);
            for (int i = 0; i < 4; i++)
            {
                float t = (i + 1) / 5f;
                Place(ItemKind.Gem, 1, CourseRampStart + CourseRampLength * t, 1.1f + roof * t);
                Place(ItemKind.Gem, 2, courseLandingStart + 1f + i * 4f, 1.1f + roof);
            }
            Place(ItemKind.Gem, 2, courseAdjacentEnd - 1f, 1.1f + roof);
            Place(ItemKind.Gem, 2, courseAdjacentEnd + CourseRoofGap * .5f, 2.35f + roof);
        }
#endif

        private void PlaceCourseBus(ObstacleDefinition bus, int lane, float start) =>
            Place(ItemKind.Obstacle, lane, start + bus.Length * .5f, 0f, bus, courseRoof: true);

        private Transform CreateCourseSurface(string name, float x, float start, float end, float low, float high, float width,
            ObstacleDefinition bus = null, Transform scenery = null, bool roofBridge = false)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visual.GetComponent<Collider>());
            visual.name = name;
            visual.transform.SetParent(scenery != null ? scenery : courseScenery, false);
            float rise = high - low;
            float length = end - start;
            visual.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(rise, length) * Mathf.Rad2Deg, 0f, 0f);
            visual.transform.localScale = new Vector3(width, .16f, Mathf.Sqrt(length * length + rise * rise));
            visual.transform.localPosition = new Vector3(x, (low + high) * .5f - .08f +
                (roofBridge ? .025f : 0f), (start + end) * .5f);
            Renderer busRenderer = (bus ?? courseBus).Prefab.GetComponentInChildren<Renderer>(true);
            Renderer surfaceRenderer = visual.GetComponent<Renderer>();
            if (busRenderer != null) surfaceRenderer.sharedMaterial = busRenderer.sharedMaterial;
            if (roofBridge)
            {
                var properties = new MaterialPropertyBlock();
                Material material = surfaceRenderer.sharedMaterial;
                Color baseColor = material != null && material.HasProperty("_Color") ? material.color : Color.gray;
                Color bridgeColor = Color.Lerp(baseColor, new Color32(235, 209, 155, 255), .35f);
                properties.SetColor("_Color", bridgeColor);
                properties.SetColor("_BaseColor", bridgeColor);
                surfaceRenderer.SetPropertyBlock(properties);
            }
            if (rise > 0f)
            {
                for (int row = 1; row <= 3; row++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        mark.name = "Flecha de subida";
                        Destroy(mark.GetComponent<Collider>());
                        mark.transform.SetParent(visual.transform, false);
                        mark.transform.localPosition = new Vector3(side * .1f, .56f, row * .22f - .44f);
                        mark.transform.localRotation = Quaternion.Euler(0f, -side * 58f, 0f);
                        mark.transform.localScale = new Vector3(.045f, .08f, .22f);
                        var tint = new MaterialPropertyBlock();
                        tint.SetColor("_Color", new Color32(255, 230, 154, 255));
                        tint.SetColor("_BaseColor", new Color32(255, 230, 154, 255));
                        mark.GetComponent<Renderer>().SetPropertyBlock(tint);
                    }
                }
            }
            return visual.transform;
        }

#if UNITY_EDITOR
        private bool SkipVehicleCourse(ref float distance)
        {
            if (courseScenery == null || distance > courseEnd + 12f) return false;
            lastPatternEnd = courseEnd;
            distance = courseEnd + 12.1f;
            return true;
        }
#endif

        private bool TrySpawnElevatedEncounter(float riderDistance)
        {
            float start = nextPatternDistance;
            if (start < nextElevatedDistance || start < nextVehicleSafeDistance || courseScenery != null) return false;
            RouteDefinition route = roadStream.Route;
            int index = route.SectionAt(start, out float sectionStart);
            RouteSection section = route.Sections[index];
            ObstacleSet catalog = section.Map.Obstacles;
            ObstacleDefinition bus = catalog?.ElevatedBus;
            VehiclePattern[] patterns = catalog?.VehiclePatterns;
            if (section.Transition || bus == null || patterns == null || patterns.Length == 0 ||
                start < sectionStart + 48f) return false;

            VehiclePattern pattern = patterns[vehiclePatternIndex % patterns.Length];
            if (pattern == VehiclePattern.ArticulatedPair) pattern = VehiclePattern.ArticulatedRoofBridge;
            if (bus.Id == "wood_container" &&
                (pattern == VehiclePattern.BusConvoy || pattern == VehiclePattern.AlternatingBuses))
                pattern = VehiclePattern.RampConvoy;
            const float joint = 1.4f;
            float articulatedLength = 2f * bus.Length + joint;
            float roofStart = start + CourseRampLength;
            // Give a roof jump enough deck to land at boost speed.
            float landing = roofStart + bus.Length + 12f;
            float laneSwitchGap = VehicleLaneSwitchGap(route, catalog);
            float end;
            switch (pattern)
            {
                case VehiclePattern.Articulated: end = start + articulatedLength; break;
                case VehiclePattern.RoofBridge: end = roofStart + bus.Length + 2f; break;
                case VehiclePattern.ArticulatedRoofBridge: end = roofStart + articulatedLength + 2f; break;
                case VehiclePattern.RoofJump: end = landing + articulatedLength + bus.Length; break;
                case VehiclePattern.RampBarricade: end = roofStart + bus.Length + 2f; break;
                case VehiclePattern.BusConvoy: end = start + 3f * bus.Length + 12f; break;
                case VehiclePattern.RampConvoy: end = roofStart + 3f * bus.Length + 2f * laneSwitchGap; break;
                case VehiclePattern.FlankedRamp: end = roofStart + 2f * bus.Length + laneSwitchGap; break;
                case VehiclePattern.AlternatingBuses: end = start + 3f * bus.Length + 18f; break;
                default: end = roofStart + articulatedLength; break;
            }
            float exitClearance = Mathf.Max(28f, laneSwitchGap);
            if (route.NeedsGentlePatterns(start - 6f, end + exitClearance) ||
                route.TouchesDecisionApproach(start - 1f, end + 1f)) return false;

            int lane = previousVehicleLane < 0 ? random.Next(3) : (previousVehicleLane + 1 + random.Next(2)) % 3;
            int neighbor = lane == 0 ? 1 : lane == 2 ? 1 : random.Next(2) * 2;
            bus = catalog.PickPatternVehicle(random);
            var scenery = new GameObject(pattern.ToString()).transform;
            scenery.SetParent(transform, false);
            scenery.localPosition = Vector3.forward * (start - riderDistance);
            var encounter = new ElevatedEncounter { Ramp = scenery, Start = start, End = end };
            elevatedEncounters.Add(encounter);
#if UNITY_EDITOR
            scenery.gameObject.SetActive(previewObstacles);
#endif

            float LaneX(int target) => (target - 1) * LaneSpacing;
            void Surface(string name, float a, float b, float x, float width, float low, float high, bool roofBridge = false)
            {
                encounter.Surfaces.Add(new VehicleSurface(a, b, x, width * .5f, low, high));
                if (name != null) CreateCourseSurface(name, x, a - start, b - start, low, high, width, bus, scenery, roofBridge);
            }
            void Bus(int target, float at, bool rideable, ObstacleDefinition body = null)
            {
                body ??= catalog.PickPatternVehicle(random);
                TrackItem item = Place(ItemKind.Obstacle, target, at - riderDistance + bus.Length * .5f, 0f, body, courseRoof: rideable);
                if (rideable) encounter.Surfaces.Add(new VehicleSurface(at, at + bus.Length,
                    LaneX(target), bus.Width * .5f, bus.Height, bus.Height, item));
            }
            void Articulated(int target, float at, bool rideable)
            {
                var body = catalog.PickPatternVehicle(random);
                Bus(target, at, rideable, body);
                float seam = at + bus.Length;
                Bus(target, seam + joint, rideable, body);
                float x = LaneX(target);
                if (rideable) Surface("Articulated joint", seam, seam + joint, x, bus.Width, bus.Height, bus.Height);
                else CreateCourseSurface("Articulated joint", x, seam - start, seam + joint - start,
                    bus.Height, bus.Height, bus.Width, bus, scenery);
            }
            void Gem(int target, float at, float height) =>
                Place(ItemKind.Gem, target, at - riderDistance, 1.1f + height);

            if (pattern == VehiclePattern.BusConvoy)
            {
                for (int i = 0; i < 3; i++) Bus(lane, start + i * (bus.Length + 6f), false);
            }
            else if (pattern == VehiclePattern.AlternatingBuses)
            {
                for (int i = 0; i < 3; i++) Bus(i % 2 == 0 ? lane : neighbor, start + i * (bus.Length + 9f), false);
            }
            else if (pattern == VehiclePattern.Articulated)
            {
                Articulated(lane, start, false);
            }
            else
            {
                Surface("Entry ramp", start, roofStart, LaneX(lane), 2f, 0f, bus.Height);
                for (int i = 1; i <= 4; i++) Gem(lane, start + CourseRampLength * i / 5f, bus.Height * i / 5f);
                switch (pattern)
                {
                    case VehiclePattern.RampConvoy:
                        Bus(lane, roofStart, true);
                        for (int i = 1; i <= 2; i++) Bus(lane, roofStart + i * (bus.Length + laneSwitchGap), false);
                        Gem(lane, roofStart + bus.Length - 1f, bus.Height);
                        break;
                    case VehiclePattern.FlankedRamp:
                        Articulated(lane, roofStart, true);
                        Bus(neighbor, roofStart, true);
                        Surface("Roof crossing", roofStart, roofStart + bus.Length,
                            (LaneX(lane) + LaneX(neighbor)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Bus(3 - lane - neighbor, roofStart + bus.Length + laneSwitchGap, false);
                        Gem(lane, roofStart + articulatedLength - 2f, bus.Height);
                        break;
                    case VehiclePattern.RoofBridge:
                        Bus(lane, roofStart, true);
                        Bus(neighbor, roofStart + 2f, true);
                        Surface("Roof crossing", roofStart + 2f, roofStart + bus.Length,
                            (LaneX(lane) + LaneX(neighbor)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Gem(neighbor, end - 1f, bus.Height);
                        break;
                    case VehiclePattern.ArticulatedRoofBridge:
                        Articulated(lane, roofStart, true);
                        Articulated(neighbor, roofStart + 2f, true);
                        Surface("Roof crossing", roofStart + 2f, roofStart + bus.Length,
                            (LaneX(lane) + LaneX(neighbor)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Gem(lane, roofStart + 1f, bus.Height);
                        Gem(neighbor, roofStart + bus.Length - 1f, bus.Height);
                        Gem(neighbor, end - 2f, bus.Height);
                        break;
                    case VehiclePattern.RoofJump:
                        Bus(lane, roofStart, true);
                        Articulated(lane, landing, true);
                        Bus(lane, landing + articulatedLength, true);
                        Gem(lane, roofStart + bus.Length - 1f, bus.Height);
                        Gem(lane, landing + 4f, bus.Height);
                        break;
                    case VehiclePattern.RampBarricade:
                        for (int i = 0; i < 3; i++) Bus(i, roofStart + (i == lane ? 0f : 2f), true);
                        for (int i = 0; i < 2; i++)
                            Surface("Roof crossing", roofStart + 2f, roofStart + bus.Length,
                                (LaneX(i) + LaneX(i + 1)) * .5f, LaneSpacing, bus.Height, bus.Height, roofBridge: true);
                        Gem(lane, roofStart + bus.Length - 1f, bus.Height);
                        break;
                    default:
                        Articulated(lane, roofStart, true);
                        Gem(lane, end - 2f, bus.Height);
                        break;
                }
            }
            vehiclePatternIndex++;
            previousVehicleLane = lane;
            lastPatternEnd = end;
            nextVehicleSafeDistance = end + exitClearance;
            nextPatternDistance = end + exitClearance;
            nextElevatedDistance = start + (section.EncounterProfile?.VehiclePatternInterval ?? ElevatedInterval);
            return true;
        }

        private void UpdateVehicleCourse(float distance, float speed, float x, float time, float dt, ref float height)
        {
            float floor = 0f;
            float roof = 0f;
            if (courseScenery != null)
            {
                courseScenery.localPosition = Vector3.back * distance;
                roof = courseBus.Height;
                float adjacentStart = CourseBusStart + 2f;
                float firstEnd = CourseBusStart + courseBus.Length;
                bool rightRoof = Mathf.Abs(x - LaneSpacing) <= 1f &&
                    ((distance >= adjacentStart && distance <= courseAdjacentEnd) ||
                     (distance >= courseLandingStart && distance <= courseEnd));
                if (Mathf.Abs(x) <= 1f && distance >= CourseRampStart && distance <= CourseBusStart)
                    floor = roof * Mathf.InverseLerp(CourseRampStart, CourseBusStart, distance);
                else if ((distance >= CourseBusStart && distance <= firstEnd && Mathf.Abs(x) <= 1f) ||
                         (distance >= adjacentStart && distance <= firstEnd && x >= 0f && x <= LaneSpacing) ||
                         rightRoof)
                    floor = roof;
            }
            for (int i = elevatedEncounters.Count - 1; i >= 0; i--)
            {
                ElevatedEncounter encounter = elevatedEncounters[i];
                if (distance > encounter.End + 12f)
                {
                    Destroy(encounter.Ramp.gameObject);
                    elevatedEncounters.RemoveAt(i);
                    continue;
                }
                Vector3 position = encounter.Ramp.localPosition;
                position.z = encounter.Start - distance;
                encounter.Ramp.localPosition = position;
                foreach (VehicleSurface surface in encounter.Surfaces)
                {
                    if (distance < surface.Start || distance > surface.End || Mathf.Abs(x - surface.X) > surface.HalfWidth)
                        continue;
                    roof = Mathf.Max(roof, surface.High);
                    floor = Mathf.Max(floor, Mathf.Lerp(surface.Low, surface.High,
                        Mathf.InverseLerp(surface.Start, surface.End, distance)));
                }
            }

            // Support a grounded rider on ramps and catch airborne riders only from above.
            float slopeStep = roof * speed * dt / CourseRampLength + .06f;
            if (height > 0f)
            {
                courseHeightOffset = courseJumpBase;
                if (courseHeightOffset + height <= floor && rider.localPosition.y >= floor - slopeStep)
                {
                    jump.Reset();
                    courseHeightOffset = floor;
                    courseFallSpeed = 0f;
                    height = 0f;
                }
            }
            else if (courseHeightOffset <= floor && floor - courseHeightOffset <= slopeStep)
            {
                courseHeightOffset = floor;
                courseFallSpeed = 0f;
            }
            else
            {
                float fallStep = dt;
                float beyondArc = jump.FallTimeAfterArc(time, dt);
                if (beyondArc >= 0f && courseJumpBase > floor && courseHeightOffset > floor)
                {
                    courseFallSpeed = JumpArc.EndDescentSpeed;
                    fallStep = beyondArc;
                }
                float drop = (courseFallSpeed + .5f * CourseFallGravity * fallStep) * fallStep;
                courseFallSpeed += CourseFallGravity * fallStep;
                float landingFloor = floor <= courseHeightOffset ? floor : 0f;
                courseHeightOffset = Mathf.Max(landingFloor, courseHeightOffset - drop);
                if (courseHeightOffset == landingFloor) courseFallSpeed = 0f;
            }
            courseJumpBase = height > 0f ? courseJumpBase : courseHeightOffset;
            height += courseHeightOffset;
        }

#if UNITY_EDITOR
        public void ConfigureArtPreview(bool gems, bool obstacles, bool showRider, bool enableCollisions)
        {
            bool visibilityChanged = gems != previewGems || obstacles != previewObstacles;
            previewGems = gems; previewObstacles = obstacles; previewInvulnerable = !enableCollisions;
            rider.gameObject.SetActive(showRider); riderShadow.gameObject.SetActive(showRider);
            if (courseScenery != null) courseScenery.gameObject.SetActive(obstacles);
            foreach (ElevatedEncounter encounter in elevatedEncounters) encounter.Ramp.gameObject.SetActive(obstacles);
            if (visibilityChanged)
                foreach (TrackItem item in items)
                    item.View.gameObject.SetActive(item.Active && (item.Kind == ItemKind.Gem ? gems : obstacles));
        }
#endif
        public void ConfigureDepthBend(bool enabled) => depthBendRequested = enabled;
        public void ConfigurePickupEffects(bool enabled)
        {
            pickupEffectsEnabled = enabled;
            if (enabled) return;
            ClearGemScatter();
            if (gemFlash != null) gemFlash.enabled = false;
            if (letterBeam != null) letterBeam.enabled = false;
            UpdateLetterMotes(null, 0f);
        }

        private void UpdateDepthBend()
        {
            if (settings == null) return;
            depthBendStart = settings.DepthBendStart;
            float strength = depthBendRequested && settings.DepthBendAvailable
                ? Mathf.Abs(settings.DepthBendStrength) * roadStream.Route.TransitionBendAt(riderDistance, settings.TransitionBendLeadDistance)
                : 0f;
            bool enabled = roadStream.ConfigureDepthBend(strength != 0f, depthBendStart, strength);
            refreshDepthBend = depthBendStrength != 0f || enabled;
            depthBendStrength = enabled ? strength : 0f;
        }
        public event Action<int, bool> GemFeedback;
        public event Action<bool, int> ImpactFeedback;
        public RouteDefinition Route => roadStream.Route;
        private enum ItemKind { Gem, Obstacle }
        private sealed class TrackItem
        {
            public Transform View;
            public float LaneX;
            public ItemKind Kind;
            public ObstacleDefinition Definition;
            public int AirborneNote;
            public bool ObstacleHit;
            public bool CourseRoof;
            public float? ImpactTime;
            public bool Active;
        }
        private readonly struct Crossing
        {
            public readonly TrackItem Item;
            public readonly float Fraction;
            public Crossing(TrackItem item, float fraction) { Item = item; Fraction = fraction; }
        }
        private static readonly Comparison<Crossing> CrossingOrder = (a, b) => a.Fraction.CompareTo(b.Fraction);

        public void Initialize(GameSettings configuration)
        {
            settings = configuration;
            if (settings.GemFlash.Sprite == null)
                Debug.LogWarning("GameSettings has no Gem Flash Sprite assigned.", this);
            cameraOrigin = worldCamera.transform.localPosition;
            cameraRotation = worldCamera.transform.localRotation;
            riderRenderers = rider.GetComponentsInChildren<Renderer>(true);
            riderMaterials = new Material[riderRenderers.Length][];
            riderFadeMaterials = new Material[riderRenderers.Length][];
            for (int i = 0; i < riderRenderers.Length; i++)
            {
                Material[] original = riderMaterials[i] = riderRenderers[i].sharedMaterials;
                riderFadeMaterials[i] = new Material[original.Length];
                for (int slot = 0; slot < original.Length; slot++)
                {
                    Material material = original[slot];
                    if (material == null || material.shader.name != "Standard")
                    {
                        riderFadeMaterials[i][slot] = material;
                        continue;
                    }
                    if (!riderFadeCopies.TryGetValue(material, out Material fade))
                    {
                        fade = new Material(material);
                        Color color = fade.color;
                        color.a *= .45f;
                        fade.color = color;
                        fade.SetOverrideTag("RenderType", "Transparent");
                        fade.SetFloat("_Mode", 2f);
                        fade.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fade.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fade.SetInt("_ZWrite", 0);
                        fade.DisableKeyword("_ALPHATEST_ON");
                        fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        fade.EnableKeyword("_ALPHABLEND_ON");
                        fade.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        riderFadeCopies.Add(material, fade);
                    }
                    riderFadeMaterials[i][slot] = fade;
                }
            }
            if (settings.RiderImpactShader != null)
            {
                riderImpactMaterial = new Material(settings.RiderImpactShader);
                riderWhiteMaterials = new Material[riderRenderers.Length][];
                for (int i = 0; i < riderRenderers.Length; i++)
                {
                    riderWhiteMaterials[i] = new Material[riderMaterials[i].Length];
                    for (int slot = 0; slot < riderWhiteMaterials[i].Length; slot++)
                        riderWhiteMaterials[i][slot] = riderImpactMaterial;
                }
            }
            roadStream.Initialize(settings.DepthBendShader);
            wheelOrigins = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) wheelOrigins[i] = wheels[i].localRotation;
            for (int i = 0; i < 72; i++) AddToPool(gemPrefab, ItemKind.Gem);
            foreach (var section in roadStream.Route.Sections) WarmSet(section.Map.Obstacles);
        }
        public void ResetTrack()
        {
            turboAura?.Clear();
            ClearGemScatter();
            riderDistance = 0f;
            ClearImpactBlink();
            random = new System.Random(settings.Seed);
            foreach (TrackItem item in items) Hide(item);
            jump.Reset(); nextPatternDistance = FirstPatternDistance; wave = 0; safeLane = visualLane = 1;
            nextProfileObstacle = 100f;
            nextVehicleSafeDistance = 0f;
            nextDecisionGemSection = 0;
            decisionGemSectionStart = 0f;
            previousVehicleLane = -1;
            encounterSection = -1;
            lastPatternEnd = 19f; // End of the opening gem row.
            ResetLetters();
            foreach (ElevatedEncounter encounter in elevatedEncounters) Destroy(encounter.Ramp.gameObject);
            elevatedEncounters.Clear();
            courseHeightOffset = courseJumpBase = courseFallSpeed = nextElevatedDistance = 0f;
            vehiclePatternIndex = 0;
            laneOrigin = laneTime = pedalAngle = 0;
            rider.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            riderShadow.localPosition = new Vector3(0, .025f, 0);
            roadStream.ResetRoad();
            for (int i = 0; i < wheels.Length; i++) wheels[i].localRotation = wheelOrigins[i];
            foreach (Transform pedal in pedals) pedal.localRotation = Quaternion.identity;
            cameraVelocity = Vector3.zero;
            boostCameraBlend = 0f;
            worldCamera.transform.SetLocalPositionAndRotation(cameraOrigin, cameraRotation);
            if (gemFlash != null) gemFlash.enabled = false;
            for (int i = 0; i < 4; i++) Place(ItemKind.Gem, 1, 10 + i * 3, 1.1f);
#if UNITY_EDITOR
            SpawnVehicleCourse();
#endif
            SetFieldOfView(60f);
        }
        public void InitializeTurboAura()
        {
            if (turboAura != null || settings.TurboAuraShader == null || settings.TurboAuraTexture == null) return;
            var aura = new GameObject("Turbo aura");
            aura.transform.SetParent(rider, false);
            turboAura = aura.AddComponent<TurboAuraController>();
            turboAura.Initialize(settings.TurboAuraShader, settings.TurboAuraTexture);
        }

        public void ClearTurboAura() => turboAura?.Clear();

        public void ClearGemScatter()
        {
            for (int i = 0; i < scatteredGemCount; i++)
                scatteredGems[i].gameObject.SetActive(false);
            scatteredGemCount = 0;
        }

        private void ScatterGems(int count, bool duringBoost)
        {
            ClearGemScatter();
            if (!pickupEffectsEnabled) return;
            count = Mathf.Min(count, duringBoost ? TurboImpactScatterLimit : NormalImpactScatterLimit);
            gemScatterOrigin = rider.localPosition + Vector3.up * .9f;
            gemScatterTime = 0f;
            for (int i = 0; i < count; i++)
            {
                // Jitter each sector of the burst so trajectories spread without forming rows.
                float angle = 2f * Mathf.PI * (i + (float)scatterRandom.NextDouble()) / count;
                float reach = Mathf.Lerp(1.4f, 4f, (float)scatterRandom.NextDouble());
                scatterTrajectories[i] = new Vector3(Mathf.Cos(angle) * reach,
                    Mathf.Lerp(.8f, 1.8f, (float)scatterRandom.NextDouble()), Mathf.Sin(angle) * reach - .8f);
                scatterSpins[i] = new Vector3(scatterRandom.Next(-300, 301),
                    scatterRandom.Next(-540, 541), scatterRandom.Next(-180, 181));
                if (scatteredGems[i] == null) scatteredGems[i] = Instantiate(gemPrefab, transform).transform;
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin, Quaternion.identity);
                scatteredGems[i].gameObject.SetActive(true);
            }
            scatteredGemCount = count;
        }

        private void AdvanceGemScatter(float dt)
        {
            if (scatteredGemCount == 0) return;
            gemScatterTime += dt;
            if (gemScatterTime >= .8f)
            {
                ClearGemScatter();
                return;
            }

            float t = gemScatterTime / .8f;
            float hop = Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI)) * (1f - t);
            for (int i = 0; i < scatteredGemCount; i++)
            {
                Vector3 trajectory = scatterTrajectories[i];
                Vector3 offset = new Vector3(trajectory.x * t, trajectory.y * hop - .7f * t, trajectory.z * t);
                scatteredGems[i].SetLocalPositionAndRotation(gemScatterOrigin + offset, Quaternion.Euler(scatterSpins[i] * t));
            }
        }

        public bool TryJump(float time) => courseFallSpeed == 0f && jump.TryStart(time);
        public void Advance(RunSession session, float dt, bool reducedMotion, bool boostCamera = false, bool boostAura = false)
        {
            AdvanceGemScatter(dt);
            if (boostAura) turboAura?.Advance(session.BoostActive, reducedMotion, dt, settings);
            else turboAura?.Clear();
            riderDistance = session.Distance;
            float oldX = rider.localPosition.x;
            if (session.Lane != visualLane)
            {
                laneOrigin = oldX; laneTime = 0; visualLane = session.Lane;
            }
            laneTime += dt;
            float targetX = (visualLane - 1) * LaneSpacing;
            float blend = Mathf.Clamp01(laneTime / settings.LaneChangeTime);
            // Smoothstep has a short fixed duration and can be redirected during a transition.
            float x = Mathf.Lerp(laneOrigin, targetX, blend * blend * (3 - 2 * blend));
            float height = jump.HeightAt(session.Elapsed);
            UpdateVehicleCourse(session.Distance, session.Speed, x, session.Elapsed, dt, ref height);
            rider.localPosition = new Vector3(x, height, 0);
            riderShadow.localPosition = new Vector3(x, .025f, 0);
            riderShadow.localScale = new Vector3(1.1f, 1, 1.8f) * (1 - height * .12f);
            float lean = reducedMotion ? 0 : Mathf.Clamp((oldX - x) / Mathf.Max(.001f, dt) * .55f, -17, 17);
            Quaternion pose = Quaternion.Euler(reducedMotion ? 0 : -height * 4, 0, lean);
            rider.localRotation = Quaternion.Slerp(rider.localRotation, pose, 18 * dt);
            float travel = session.Speed * dt;
            foreach (Transform wheel in wheels) wheel.Rotate(Vector3.right, travel * Mathf.Rad2Deg / .45f, Space.Self);
            pedalAngle += travel * 35;
            for (int i = 0; i < pedals.Length; i++) pedals[i].localRotation = Quaternion.Euler(Mathf.Sin((pedalAngle + i * 180) * Mathf.Deg2Rad) * 22, 0, 0);
            roadStream.Advance(session.Distance);
            crossings.Clear();
            foreach (TrackItem item in items)
            {
                if (!item.Active) continue;
                float previousZ = item.View.localPosition.z;
                item.View.localPosition += Vector3.back * travel;
                if (item.ImpactTime.HasValue && item.Definition.TumblesOnImpact)
                {
                    float progress = Mathf.Clamp01((session.Elapsed - item.ImpactTime.Value) / .4f);
                    float direction = item.LaneX < 0 ? -1f : 1f;
                    Vector3 position = item.View.localPosition;
                    position.x = item.LaneX + direction * 1.8f * progress;
                    position.y = 2.4f * progress * (1f - progress);
                    item.View.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, 0f, -direction * 150f * progress));
                }
                if (item.Kind == ItemKind.Gem)
                {
                    if (previousZ >= 0 && item.View.localPosition.z < 0)
                        crossings.Add(new Crossing(item, Mathf.Clamp01(previousZ / travel)));
                }
                else if (!item.ObstacleHit && TryObstacleContact(item, previousZ, travel, oldX, x, session.Elapsed, dt, out float fraction))
                    crossings.Add(new Crossing(item, fraction));
            }
            crossings.Sort(CrossingOrder);
            foreach (Crossing crossing in crossings)
            {
                if (session.State != RunState.Running) break;
                TrackItem item = crossing.Item;
                float fraction = crossing.Fraction;
                float crossingX = Mathf.Lerp(oldX, x, fraction);
                float crossingHeight = jump.HeightAt(session.Elapsed - dt + dt * fraction);
                crossingHeight += courseHeightOffset;
                bool sameLane = Mathf.Abs(item.LaneX - crossingX) < .83f;
                if (sameLane && item.Kind == ItemKind.Gem && JumpArc.CanCollect(crossingHeight, item.View.localPosition.y))
                {
                    bool turboWasReady = session.CanActivateTurbo;
                    session.CollectGem(item.AirborneNote);
                    if (item.View.gameObject.activeInHierarchy)
                        EmitPickupFlash(transform.TransformPoint(new Vector3(item.LaneX, item.View.localPosition.y, 0)), settings.GemFlash);
                    Hide(item);
                    GemFeedback?.Invoke(item.AirborneNote, !turboWasReady && session.CanActivateTurbo);
                }
                else if (item.Kind == ItemKind.Obstacle
#if UNITY_EDITOR
                    && !previewInvulnerable
#endif
                )
                {
                    item.ObstacleHit = true;
                    // Vehicles stay clear of the camera after contact. Small obstacles tumble aside.
                    if (!item.Definition.TumblesOnImpact)
                    {
                        if (item.CourseRoof)
                            foreach (ElevatedEncounter encounter in elevatedEncounters)
                                for (int i = encounter.Surfaces.Count - 1; i >= 0; i--)
                                    if (encounter.Surfaces[i].Vehicle == item) encounter.Surfaces.RemoveAt(i);
                        Hide(item);
                    }
                    if (session.IsProtected) continue;
                    item.ImpactTime = session.Elapsed;
                    bool duringBoost = session.BoostActive;
                    int previousGemsLost = session.GemsLost;
                    session.Hit(item.Definition.Id);
                    int gemsLost = session.GemsLost - previousGemsLost;
                    if (duringBoost)
                    {
                        turboAura?.Clear();
                        var position = worldCamera.transform.localPosition;
                        position.z = cameraOrigin.z;
                        worldCamera.transform.localPosition = position;
                        cameraVelocity.z = 0f;
                        SetFieldOfView(Mathf.Max(60f, referenceFieldOfView - settings.BoostCameraFieldOfView * boostCameraBlend));
                        boostCameraBlend = 0f;
                    }
                    if (gemsLost > 0) ScatterGems(gemsLost, duringBoost);
                    ImpactFeedback?.Invoke(duringBoost, gemsLost);
                }
            }
            foreach (TrackItem item in items)
            {
                float behind = item.Definition != null ? -item.Definition.Length * .5f - 9 : -8;
                if (item.Active && item.View.localPosition.z < behind) Hide(item);
            }
            // Speed changes the reveal horizon while the route positions stay fixed.
            float lookAhead = Mathf.Max(settings.MinimumPatternLookAhead, session.Speed * settings.PreviewTime);
            if (settings.PreparePatternsBeyondVisibility)
                lookAhead = Mathf.Max(lookAhead, roadStream.VisibilityDistance + settings.PatternVisibilityMargin);
            float spawnHorizon = session.Distance + lookAhead;
            AdvanceLetter(travel, oldX, x, session, dt);
            SpawnDecisionGems(spawnHorizon, session.Distance);
            float clearance = Mathf.Max(0f, letterClearance);
            while (true)
            {
                float letterDistance = Mathf.Max(nextLetterDistance, lastPatternEnd + clearance);
                if (nextLetterDistance <= nextPatternDistance && CanPlaceLetter(letterDistance, clearance))
                {
                    if (letterDistance > spawnHorizon) break;
                    if (TryPlaceLetter(letterDistance, session.Distance))
                    {
                        lastPatternEnd = letterDistance;
                        nextPatternDistance = Mathf.Max(nextPatternDistance, letterDistance + clearance);
                    }
                }
                if (nextPatternDistance > spawnHorizon) break;
#if UNITY_EDITOR
                if (SkipVehicleCourse(ref nextPatternDistance)) continue;
#endif

                if (TrySpawnElevatedEncounter(session.Distance) || TrySpawnProfileEncounter(session.Distance)) continue;
                if (nextPatternDistance < nextVehicleSafeDistance)
                {
                    nextPatternDistance = nextVehicleSafeDistance;
                    continue;
                }
                lastPatternEnd = SpawnPattern(nextPatternDistance, session.Distance);
                nextPatternDistance = Mathf.Max(nextPatternDistance + settings.PatternSpacingAt(nextPatternDistance),
                    lastPatternEnd + settings.PatternClearance);
            }
            session.ShowSpeedOffer();
            float protection = session.ProtectionProgress;
            const float whiteFlashEnd = .07f;
            bool showImpact = !reducedMotion && session.IsProtected;
            SetRiderImpactWhite(showImpact && protection < whiteFlashEnd);
            float blinkPhase = (protection - whiteFlashEnd) * 3f / (1f - whiteFlashEnd);
            SetRiderImpactTransparent(showImpact && blinkPhase >= 0f && Mathf.Repeat(blinkPhase, 1f) >= .65f);
            float boostTarget = session.BoostActive && !reducedMotion ? 1f : 0f;
            boostCameraBlend = reducedMotion || !boostCamera ? 0f :
                Mathf.Lerp(boostCameraBlend, boostTarget, 1f - Mathf.Exp(-settings.BoostCameraBlendRate * dt));
            FollowRider(x, reducedMotion, dt);
            if (session.IsProtected && !reducedMotion) return;
            float pace = session.Speed - (session.BoostActive ? RunSession.BoostSpeed : 0f);
            float intensity = Mathf.InverseLerp(settings.InitialSpeed, settings.MaximumSpeed, pace);
            float targetFieldOfView = reducedMotion ? 60f : Mathf.Lerp(60f, 68f, intensity) +
                settings.BoostCameraFieldOfView * boostCameraBlend;
            SetFieldOfView(Mathf.Lerp(referenceFieldOfView, targetFieldOfView, 1f - Mathf.Exp(-10f * dt)));
        }

        private void FollowRider(float x, bool reducedMotion, float dt)
        {
            Vector3 cameraTarget = cameraOrigin + (reducedMotion ? Vector3.zero : Vector3.right * (x * settings.CameraFollowStrength));
            cameraTarget.y += courseHeightOffset;
            cameraTarget += Vector3.back * (settings.BoostCameraPullback * boostCameraBlend);
            worldCamera.transform.localPosition = Vector3.SmoothDamp(worldCamera.transform.localPosition, cameraTarget, ref cameraVelocity, settings.CameraFollowTime, Mathf.Infinity, dt);
        }

        public void ClearImpactBlink()
        {
            SetRiderImpactWhite(false);
            SetRiderImpactTransparent(false);
        }

        private void SetRiderImpactTransparent(bool transparent)
        {
            if (riderImpactTransparent == transparent) return;
            riderImpactTransparent = transparent;
            ApplyRiderImpactMaterials();
        }

        private void SetRiderImpactWhite(bool white)
        {
            if (riderImpactMaterial == null || riderImpactWhite == white) return;
            riderImpactWhite = white;
            ApplyRiderImpactMaterials();
        }

        private void ApplyRiderImpactMaterials()
        {
            Material[][] materials = riderImpactWhite ? riderWhiteMaterials :
                riderImpactTransparent ? riderFadeMaterials : riderMaterials;
            for (int i = 0; i < riderRenderers.Length; i++)
                riderRenderers[i].sharedMaterials = materials[i];
        }

        private void OnDestroy()
        {
            if (riderImpactMaterial != null) Destroy(riderImpactMaterial);
            foreach (Material fade in riderFadeCopies.Values) Destroy(fade);
        }
        /// <summary>Scale upcoming profiled encounters independently. Existing encounters stay in place.</summary>
        public void SetEncounterIntensity(float gems, float obstacles)
        {
            gemIntensity = Mathf.Clamp(gems, .25f, 4f);
            obstacleIntensity = Mathf.Clamp(obstacles, .25f, 4f);
        }

        private float nextProfileGems;

        private void SpawnDecisionGems(float horizon, float distance)
        {
            RouteDefinition route = roadStream.Route;
            while (nextDecisionGemSection < route.Sections.Length)
            {
                float length = route.Sections[nextDecisionGemSection].ModuleCount * RoadStandard.ModuleLength;
                float midpoint = decisionGemSectionStart + length * .5f;
                float lead = Mathf.Min(24f, length * .25f);
                if (midpoint - lead > horizon) return;
                if (route.HasDecision(nextDecisionGemSection))
                {
                    for (int i = 1; i <= 6; i++)
                        Place(ItemKind.Gem, 1, midpoint - lead * (1f - i / 7f) - distance, 1.1f);
                }
                nextDecisionGemSection++;
                decisionGemSectionStart += length;
            }
        }

        private bool TrySpawnProfileEncounter(float riderDistance)
        {
            float distance = nextPatternDistance;
            RouteDefinition route = roadStream.Route;
            int index = route.SectionAt(distance, out float start);
            RouteSection section = route.Sections[index];
            EncounterProfile profile = section.EncounterProfile;
            if (profile == null) return false;

            float end = index == route.Sections.Length - 1
                ? float.PositiveInfinity : start + section.ModuleCount * RoadStandard.ModuleLength;
            if (index != encounterSection)
            {
                encounterSection = index;
                nextProfileGems = distance;
                nextProfileObstacle = Mathf.Max(100f, distance + 6f);
            }
            // Earlier encounters, letters and decisions can advance the shared cursor.
            bool spawnGems = nextProfileGems <= nextProfileObstacle;
            if (!spawnGems && distance < nextVehicleSafeDistance)
            {
                nextProfileObstacle = Mathf.Max(nextProfileObstacle, nextVehicleSafeDistance);
                nextPatternDistance = Mathf.Min(end, Mathf.Max(distance, Mathf.Min(nextProfileGems, nextProfileObstacle)));
                return true;
            }
            float occupiedEnd = distance;
            if (spawnGems)
            {
                int count = profile.GemsPerThread;
                occupiedEnd = distance + (count - 1) * profile.GemSpacing;
                if (occupiedEnd + 2f < end && !route.TouchesDecisionApproach(distance, occupiedEnd))
                {
                    safeLane = Mathf.Clamp(safeLane + random.Next(-1, 2), 0, 2);
                    int threads = profile.GemThreads;
                    if (threads > 1 && profile.ParallelThreadChance < 1f &&
                        random.NextDouble() >= profile.ParallelThreadChance) threads = 1;
                    int lane = safeLane;
                    int turnAt = threads == 1 && count >= 5 && profile.LaneChangeChance > 0f &&
                        random.NextDouble() < profile.LaneChangeChance ? random.Next(2, count - 1) : -1;
                    for (int gem = 0; gem < count; gem++)
                    {
                        if (gem == turnAt)
                        {
                            lane += lane == 1 ? (random.Next(2) == 0 ? -1 : 1) : 1 - lane;
                            // Leave one slot empty so the lane change fits at boost speed.
                            continue;
                        }
                        for (int thread = 0; thread < threads; thread++)
                            Place(ItemKind.Gem, (lane + thread) % 3,
                                distance - riderDistance + gem * profile.GemSpacing, 1.1f);
                    }
                    safeLane = lane;
                }
                float variation = profile.GemIntervalVariation;
                float jitter = variation > 0f ? 1f + variation * (2f * (float)random.NextDouble() - 1f) : 1f;
                nextProfileGems = distance + profile.GemInterval * jitter / gemIntensity;
            }
            else
            {
                ObstacleSet catalog = obstacleOverride != null ? obstacleOverride : section.Map.Obstacles;
                // Leave a free path and enough distance to read and change lanes.
                float clearance = Mathf.Max(profile.ObstacleClearance, VehicleLaneSwitchGap(route, catalog));
                float maximumLength = Mathf.Min(distance < 220f ? 4.5f : float.PositiveInfinity, end - distance - clearance);
                int lane = (safeLane + 1 + random.Next(2)) % 3;
                float occupied = PlaceObstacle(catalog, wave % 3 == 0, lane, distance - riderDistance, maximumLength);
                if (occupied > 0f && catalog?.ElevatedBus != null && wave % 2 == 1)
                {
                    int otherLane = 3 - safeLane - lane;
                    occupied = Mathf.Max(occupied,
                        PlaceObstacle(catalog, false, otherLane, distance - riderDistance, maximumLength));
                }
                occupiedEnd = distance + occupied;
                if (occupied > 0f)
                {
                    nextVehicleSafeDistance = occupiedEnd + clearance;
                    nextProfileGems = Mathf.Max(nextProfileGems, occupiedEnd + Mathf.Max(8f, clearance * .5f));
                }
                nextProfileObstacle = Mathf.Max(distance + profile.ObstacleInterval / obstacleIntensity, occupiedEnd + clearance);
                wave++;
            }
            lastPatternEnd = Mathf.Min(end, occupiedEnd);
            nextPatternDistance = Mathf.Min(end, Mathf.Max(Mathf.Min(nextProfileGems, nextProfileObstacle), occupiedEnd + 2f));
            return true;
        }

        private float VehicleLaneSwitchGap(RouteDefinition route, ObstacleSet catalog = null)
        {
            float topSpeed = settings.MaximumSpeed + route.FinalSectionSpeedBonus +
                route.MaximumDecisionSpeed + RunSession.BoostSpeed;
            // Allow time to read the second row and make up to two lane changes.
            return topSpeed * (settings.LaneChangeTime * 2f +
                (catalog?.ElevatedBus?.Id == "wood_container" ? .8f : .25f)) + 1.2f;
        }

        private float SpawnPattern(float distance, float riderDistance)
        {
            safeLane = Mathf.Clamp(safeLane + random.Next(-1, 2), 0, 2);
            float z = distance - riderDistance;
            float spacing = settings.GemSpacingAt(distance);
            float patternEnd = distance + spacing * 3;
            if (roadStream.Route.TouchesDecisionApproach(distance, patternEnd)) return patternEnd;
            bool gentle = roadStream.Route.NeedsGentlePatterns(distance - 6, patternEnd + 6);
            bool jumping = !gentle && distance > 155f && wave % 4 == 2;
            for (int i = 0; i < 4; i++)
                Place(ItemKind.Gem, safeLane, z + i * spacing, jumping ? (i == 0 || i == 3 ? 2.05f : 2.7f) : 1.1f,
                    airborneNote: jumping ? i : -1);
            ObstacleSet obstacleSet = obstacleOverride != null ? obstacleOverride : roadStream.Route.MapAtDistance(distance).Obstacles;
            float maximumLength = gentle || distance < 220f ? 4.5f : float.PositiveInfinity;
            float obstacleOffset = spacing * 1.5f;
            float obstacleEnd = distance + obstacleOffset;
            if (jumping) obstacleEnd += PlaceObstacle(obstacleSet, false, safeLane, z + obstacleOffset, maximumLength);
            if (distance > 100f)
            {
                int blockedLane = (safeLane + 1 + random.Next(2)) % 3;
                obstacleEnd = Mathf.Max(obstacleEnd, distance + obstacleOffset +
                    PlaceObstacle(obstacleSet, wave % 3 == 0, blockedLane, z + obstacleOffset, maximumLength));
                if (!gentle && !jumping && distance > 300f && wave % 3 == 1)
                {
                    int otherLane = 3 - safeLane - blockedLane;
                    obstacleEnd = Mathf.Max(obstacleEnd, distance + obstacleOffset +
                        PlaceObstacle(obstacleSet, false, otherLane, z + obstacleOffset, maximumLength));
                }
            }
            // A jump row blocks at most two lanes; every other row keeps the gem lane clear.
            wave++;
            return Mathf.Max(patternEnd, obstacleEnd + VehicleLaneSwitchGap(roadStream.Route));
        }

        private void ResetLetters()
        {
            if (letterPickup != null) letterPickup.gameObject.SetActive(false);
            if (letterBeam != null) letterBeam.enabled = false;
            activeLetter = -1;
            activeWord = null;
            pendingLetters.Clear();
            nextLetterDistance = 100f;
            scheduledLettersPlaced = 0;
            scheduledLetterStart = scheduledLetterEnd = 0f;
            RouteDefinition route = roadStream.Route;
            if (route.LastLetterFinalSectionModule > 0 && route.Sections.Length > 1)
            {
                float moduleLength = RoadStandard.ModuleLength;
                scheduledLetterStart = route.Sections[0].ModuleCount * moduleLength;
                if (route.Sections[1].Transition) scheduledLetterStart += route.Sections[1].ModuleCount * moduleLength;
                scheduledLetterStart += letterClearance;
                for (int i = 0; i < route.Sections.Length - 1; i++)
                {
                    if (route.LastLetterZone != null && route.Sections[i].Map == route.LastLetterZone &&
                        !route.Sections[i].Transition) break;
                    scheduledLetterEnd += route.Sections[i].ModuleCount * moduleLength;
                }
                scheduledLetterEnd += route.LastLetterFinalSectionModule * moduleLength;
                nextLetterDistance = scheduledLetterStart;
            }
        }

        private bool PrepareWord()
        {
            var font = LetterFont;
            if (font == null || collectibleWords == null) return false;

            for (int attempt = 0; attempt < collectibleWords.Length; attempt++)
            {
                int index = nextWord % collectibleWords.Length;
                nextWord = index + 1;
                string word = collectibleWords[index]?.Trim().Normalize().ToUpperInvariant();
                if (string.IsNullOrEmpty(word)) continue;
                if (Array.Exists(word.ToCharArray(), c => c != ' ' && !font.HasCharacter(c)))
                {
                    Debug.LogWarning($"Word collection skipped '{word}' because the font is missing a character.", this);
                    continue;
                }
                activeWord = word;
                collectedLetters = new bool[word.Length];
                var positions = new List<int>(word.Length);
                for (int i = 0; i < word.Length; i++)
                    if (word[i] != ' ') positions.Add(i);
                for (int i = positions.Count - 1; i > 0; i--)
                {
                    int j = letterRandom.Next(i + 1);
                    (positions[i], positions[j]) = (positions[j], positions[i]);
                }
                foreach (int position in positions) pendingLetters.Enqueue(position);
                return true;
            }
            return false;
        }

        private bool CanPlaceLetter(float distance, float clearance) =>
            wordCollectionEnabled && activeLetter < 0 && (activeWord == null || pendingLetters.Count > 0) &&
            !roadStream.Route.NeedsGentlePatterns(distance - clearance, distance + clearance) &&
            !roadStream.Route.TouchesDecisionApproach(distance, distance);

        private bool TryPlaceLetter(float distance, float riderDistance)
        {
            if (activeWord == null && !PrepareWord())
            {
                nextLetterDistance = float.PositiveInfinity;
                return false;
            }
            if (pendingLetters.Count == 0) return false;

            activeLetter = pendingLetters.Dequeue();
            SetLetterText(activeWord[activeLetter]);
            // The reserved gap clears all lanes, so choose a lane other than the previous gem row.
            int letterLane = (safeLane + letterRandom.Next(1, 3)) % 3;
            letterLaneX = (letterLane - 1) * LaneSpacing;
            letterHoverTime = 0f;
            letterBeamTime = 0f;
            letterPickup.transform.SetLocalPositionAndRotation(
                new Vector3(letterLaneX, LetterPickupHeight, distance - riderDistance),
                Quaternion.identity);
            letterPickup.gameObject.SetActive(true);
            UpdateLetterBeam(letterPickup.transform.localPosition, 0f);
            scheduledLettersPlaced++;
            int letterCount = 0;
            foreach (char character in activeWord)
                if (character != ' ') letterCount++;
            nextLetterDistance = scheduledLetterEnd > scheduledLetterStart && scheduledLettersPlaced < letterCount
                ? Mathf.Max(distance + 20f, Mathf.Lerp(scheduledLetterStart, scheduledLetterEnd,
                    scheduledLettersPlaced / (float)(letterCount - 1)))
                : distance + Mathf.Max(20f, letterInterval);
            return true;
        }

        private void SetLetterText(char character)
        {
            bool createText = letterPickup == null;
            if (createText)
            {
                var visual = new GameObject("Letter pickup", typeof(TMPro.TextMeshPro));
                visual.transform.SetParent(transform, false);
                letterPickup = visual.GetComponent<TMPro.TextMeshPro>();
                letterPickup.fontSize = 20f;
                letterPickup.fontStyle = TMPro.FontStyles.Bold;
                letterPickup.alignment = TMPro.TextAlignmentOptions.Center;
                letterPickup.rectTransform.sizeDelta = new Vector2(4f, 4f);
                letterPickup.extraPadding = true;
                letterPickup.enableVertexGradient = true;
                Color top = new Color32(255, 226, 89, 255);
                Color bottom = new Color32(255, 169, 52, 255);
                letterPickup.colorGradient = new TMPro.VertexGradient(top, top, bottom, bottom);
            }

            var font = LetterFont;
            if (createText || letterPickup.font != font)
            {
                letterPickup.font = font;
                if (letterOutlineMaterial != null && letterOutlineMaterial.mainTexture == font.atlasTexture)
                    letterPickup.fontSharedMaterial = letterOutlineMaterial;
                else
                {
                    letterPickup.outlineColor = Color.black;
                    letterPickup.outlineWidth = .3f;
                    Material material = letterPickup.fontMaterial;
                    material.SetFloat("_FaceDilate", .3f);
                    material.EnableKeyword("OUTLINE_ON");
                    letterPickup.UpdateMeshPadding();
                }
            }
            letterPickup.text = character.ToString();
        }
        private void AdvanceLetter(float travel, float oldX, float x, RunSession session, float dt)
        {
            if (activeLetter < 0) return;
            Vector3 position = letterPickup.transform.localPosition;
            float nextZ = position.z - travel;
            if (wordCollectionEnabled && nextZ >= 0f)
            {
                float cycle = Mathf.Max(.1f, letterHoverCycle);
                letterHoverTime = Mathf.Repeat(letterHoverTime + dt, cycle);
                float phase = letterHoverTime / cycle;
                float hover = .5f - .5f * Mathf.Cos(4f * Mathf.PI * phase);
                float tilt = Mathf.SmoothStep(0f, 1f, phase * 4f)
                    - Mathf.SmoothStep(0f, 1f, (phase - .5f) * 4f);
                letterPickup.transform.SetLocalPositionAndRotation(
                    new Vector3(letterLaneX, LetterPickupHeight + hover * letterHoverHeight, nextZ),
                    Quaternion.AngleAxis(tilt * letterTilt, Vector3.forward));
                UpdateLetterBeam(letterPickup.transform.localPosition, dt);
                return;
            }

            int index = activeLetter;
            activeLetter = -1;
            letterPickup.gameObject.SetActive(false);
            if (letterBeam != null) letterBeam.enabled = false;
            float fraction = travel > 0f ? Mathf.Clamp01(position.z / travel) : 0f;
            float height = jump.HeightAt(session.Elapsed - dt + dt * fraction);
            if (!wordCollectionEnabled || Mathf.Abs(letterLaneX - Mathf.Lerp(oldX, x, fraction)) >= .83f ||
                !JumpArc.CanCollect(height, LetterPickupHeight))
            {
                pendingLetters.Enqueue(index);
                return;
            }
            collectedLetters[index] = true;
            session.RecordLetter(activeWord, index, pendingLetters.Count == 0);
            EmitPickupFlash(transform.TransformPoint(new Vector3(letterLaneX, position.y, 0f)), settings.LetterFlash);
            LetterCollected?.Invoke(activeWord, collectedLetters, pendingLetters.Count == 0);
        }

        private void UpdateLetterBeam(Vector3 letterPosition, float dt)
        {
            Sprite sprite = pickupEffectsEnabled ? settings.LetterBeamSprite : null;
            if (sprite == null)
            {
                if (letterBeam != null) letterBeam.enabled = false;
                UpdateLetterMotes(null, 0f);
                return;
            }
            if (letterBeam == null)
            {
                var visual = new GameObject("Letter beam", typeof(SpriteRenderer));
                visual.transform.SetParent(transform, false);
                letterBeam = visual.GetComponent<SpriteRenderer>();
                letterBeamProperties = new MaterialPropertyBlock();
            }

            float duration = settings.LetterBeamPulseDuration;
            letterBeamTime = Mathf.Repeat(letterBeamTime + dt, duration);
            // Traverse the curves in both directions to return smoothly to the initial state.
            float phase = Mathf.PingPong(2f * letterBeamTime / duration, 1f);
            Bounds bounds = sprite.bounds;
            float baseScale = settings.LetterBeamBaseScale;
            float heightPulse = settings.LetterBeamHeightPulse * Mathf.Clamp01(settings.LetterBeamHeightCurve.Evaluate(phase));
            float verticalScale = baseScale + heightPulse / bounds.size.y;
            letterBeam.sprite = sprite;
            letterBeam.sortingOrder = settings.LetterBeamInFront ? 1 : -1;
            if (settings.LetterBeamMaterial != null)
            {
                letterBeam.sharedMaterial = settings.LetterBeamMaterial;
                letterBeam.GetPropertyBlock(letterBeamProperties);
                letterBeamProperties.SetColor(FlashTopTint, settings.LetterBeamTopTint);
                letterBeamProperties.SetColor(FlashBottomTint, settings.LetterBeamBottomTint);
                letterBeamProperties.SetFloat(FlashRadialGradient, 0f);
                letterBeam.SetPropertyBlock(letterBeamProperties);
            }
            letterBeam.transform.localScale = new Vector3(baseScale, verticalScale, 1f);
            Vector3 basePosition = new Vector3(letterPosition.x, 0f, letterPosition.z) + settings.LetterBeamBaseOffset;
            letterBeam.transform.localPosition = basePosition - Vector3.up * (bounds.min.y * verticalScale);
            letterBeam.transform.rotation = Quaternion.Euler(0f, worldCamera.transform.eulerAngles.y, 0f);
            Color color = Color.white;
            color.a *= Mathf.Lerp(settings.LetterBeamMinOpacity, settings.LetterBeamMaxOpacity,
                settings.LetterBeamOpacityCurve.Evaluate(phase));
            letterBeam.color = color;
            letterBeam.enabled = true;
            UpdateLetterMotes(settings.LetterMoteSprite, letterBeamTime / duration);
        }

        private void UpdateLetterMotes(Sprite sprite, float phase)
        {
            for (int i = 0; i < letterMotes.Length; i++)
            {
                SpriteRenderer mote = letterMotes[i];
                if (sprite == null)
                {
                    if (mote != null) mote.enabled = false;
                    continue;
                }
                if (mote == null)
                {
                    var visual = new GameObject("Letter mote", typeof(SpriteRenderer));
                    visual.transform.SetParent(letterPickup.transform, false);
                    mote = letterMotes[i] = visual.GetComponent<SpriteRenderer>();
                    mote.sortingOrder = 2;
                }
                float angle = (phase + (float)i / letterMotes.Length) * 2f * Mathf.PI;
                mote.transform.localPosition = new Vector3(.85f * Mathf.Cos(angle),
                    .52f * Mathf.Sin(2f * angle + i), .42f * Mathf.Sin(angle));
                mote.transform.rotation = worldCamera.transform.rotation;
                float scale = (.65f + .25f * Mathf.Sin(angle + i)) * settings.LetterMoteScaleMultiplier;
                mote.transform.localScale = Vector3.one * scale;
                Color color = settings.LetterMoteTint;
                color.a *= Mathf.Lerp(.28f, .7f, .5f + .5f * Mathf.Cos(angle - i));
                mote.sprite = sprite;
                mote.color = color;
                mote.enabled = true;
            }
        }
        private void AddToPool(GameObject prefab, ItemKind kind, ObstacleDefinition definition = null)
        {
            GameObject instance = Instantiate(prefab, transform); instance.SetActive(false);
            if (definition != null) instance.GetComponent<ObstacleView>().Apply(definition);
            items.Add(new TrackItem { View = instance.transform, Kind = kind, Definition = definition });
        }
        private TrackItem Place(ItemKind kind, int lane, float z, float height, ObstacleDefinition definition = null,
            int airborneNote = -1, bool courseRoof = false)
        {
            TrackItem item = null;
            foreach (TrackItem candidate in items)
            {
                if (candidate.Active || candidate.Kind != kind || candidate.Definition != definition) continue;
                item = candidate;
                break;
            }
            if (item == null)
            {
                AddToPool(definition == null ? gemPrefab : definition.Prefab.gameObject, kind, definition);
                item = items[items.Count - 1];
            }

            item.Active = true;
            item.ObstacleHit = false;
            item.CourseRoof = courseRoof;
            item.ImpactTime = null;
            item.AirborneNote = airborneNote;
            item.LaneX = (lane - 1) * LaneSpacing;
            item.View.SetLocalPositionAndRotation(new Vector3(item.LaneX, kind == ItemKind.Gem ? height : 0, z), Quaternion.identity);
            bool visible = true;
#if UNITY_EDITOR
            visible = kind == ItemKind.Gem ? previewGems : previewObstacles;
#endif
            item.View.gameObject.SetActive(visible);
            return item;
        }
        /// <summary>Override map selection for an event or experiment; pass null to restore map-defined traffic.</summary>
        public void SetObstacleOverride(ObstacleSet replacement)
        {
            WarmSet(replacement); obstacleOverride = replacement;
        }
        private void WarmSet(ObstacleSet set)
        {
            if (set == null) return;
            if (set.Low != null) foreach (var definition in set.Low) WarmObstacle(definition);
            if (set.Traffic != null) foreach (var definition in set.Traffic) WarmObstacle(definition);
            WarmObstacle(set.ElevatedBus);
        }
        private void WarmObstacle(ObstacleDefinition definition)
        {
            if (definition == null || !registeredObstacles.Add(definition)) return;
            for (int i = 0; i < definition.PoolCapacity; i++) AddToPool(definition.Prefab.gameObject, ItemKind.Obstacle, definition);
        }
        private float PlaceObstacle(ObstacleSet set, bool vehicle, int lane, float z, float maximumLength)
        {
            if (set == null) return 0;
            ObstacleDefinition definition = set.Pick(random, vehicle, maximumLength, vehicle && wave % 3 == 0 && float.IsPositiveInfinity(maximumLength));
            if (definition == null) return 0;
            float padding = definition.ContactPadding;
            float start = riderDistance + z;
            if (roadStream.Route.TouchesDecisionApproach(start, start + definition.Length + 2f * padding)) return 0f;
            Place(ItemKind.Obstacle, lane, z + definition.Length * .5f + padding, 0, definition);
            // Contact starts at z and ends after the full padded collider passes.
            return definition.Length + 2f * padding;
        }
        private bool TryObstacleContact(TrackItem item, float previousZ, float travel, float oldX, float newX, float elapsed, float dt, out float fraction)
        {
            fraction = 0;
            ObstacleDefinition obstacle = item.Definition;
            // The ramps meet the physical ends of the prototype buses.
            float padding = item.CourseRoof ? 0f : obstacle.ContactPadding;
            float front = obstacle.Length * .5f + padding, rear = -front;
            float currentZ = item.View.localPosition.z;
            if (previousZ < rear || currentZ > front || travel <= 0) return false;
            float enter = Mathf.Clamp01((previousZ - front) / travel), exit = Mathf.Clamp01((previousZ - rear) / travel);
            float halfWidth = obstacle.Width * .5f + .27f;
            float minimumX = item.LaneX - halfWidth, maximumX = item.LaneX + halfWidth;
            float firstX = Mathf.Lerp(oldX, newX, enter), lastX = Mathf.Lerp(oldX, newX, exit);
            if (Mathf.Max(firstX, lastX) < minimumX || Mathf.Min(firstX, lastX) > maximumX) return false;
            fraction = enter;
            if (firstX < minimumX || firstX > maximumX)
            {
                float boundary = firstX < minimumX ? minimumX : maximumX;
                fraction = Mathf.Clamp((boundary - oldX) / (newX - oldX), enter, exit);
            }
            if (obstacle.Jumpable)
            {
                float firstHeight = jump.HeightAt(elapsed - dt + dt * fraction);
                float lastHeight = jump.HeightAt(elapsed - dt + dt * exit);
                firstHeight += courseHeightOffset;
                lastHeight += courseHeightOffset;
                if (Mathf.Min(firstHeight, lastHeight) >= obstacle.JumpClearance) return false;
                if (firstHeight >= obstacle.JumpClearance) fraction = exit;
            }
            if (item.CourseRoof && rider.localPosition.y >= obstacle.Height - .05f) return false;
            // The entire vehicle body remains dangerous until its rear passes the rider.
            return true;
        }
        private void EmitPickupFlash(Vector3 position, PickupFlashSettings flash)
        {
            if (!pickupEffectsEnabled) return;
            if (flash.Sprite == null) return;
            if (gemFlash == null)
            {
                var visual = new GameObject("Gem flash", typeof(SpriteRenderer));
                visual.transform.SetParent(transform, false);
                gemFlash = visual.GetComponent<SpriteRenderer>();
                gemFlashProperties = new MaterialPropertyBlock();
                defaultFlashMaterial = gemFlash.sharedMaterial;
            }
            activeFlash = flash;
            gemFlash.sprite = flash.Sprite;
            gemFlash.sharedMaterial = flash.Material != null ? flash.Material : defaultFlashMaterial;
            gemFlash.GetPropertyBlock(gemFlashProperties);
            gemFlashProperties.SetColor(FlashTopTint, flash.TopTint);
            gemFlashProperties.SetColor(FlashBottomTint, flash.BottomTint);
            gemFlashProperties.SetFloat(FlashRadialGradient, flash.GradientMode == FlashGradientMode.Radial ? 1f : 0f);
            gemFlash.SetPropertyBlock(gemFlashProperties);
            float maxRoll = flash.RandomRotation;
            gemFlashRoll = Quaternion.AngleAxis(UnityEngine.Random.Range(-maxRoll, maxRoll), Vector3.forward);
            Vector3 flashPosition = position + transform.TransformDirection(flash.Offset);
            gemFlash.transform.SetPositionAndRotation(flashPosition, worldCamera.transform.rotation * gemFlashRoll);
            gemFlash.transform.localScale = Vector3.one * (flash.StartScale * flash.Size);
            gemFlash.color = new Color(1f, 1f, 1f, 0f);
            gemFlash.enabled = true;
            gemFlashStarted = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            UpdateDepthBend();
            if (refreshDepthBend)
            {
                foreach (TrackItem item in items)
                {
                    if (!item.Active) continue;
                    BendPickup(item.View, item.LaneX);
                    if (item.Kind == ItemKind.Obstacle)
                    {
                        float distance = Mathf.Max(0f, item.View.localPosition.z - depthBendStart);
                        float yaw = Mathf.Atan(2f * depthBendStrength * distance) * Mathf.Rad2Deg;
                        item.View.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
                if (activeLetter >= 0)
                {
                    BendPickup(letterPickup.transform, letterLaneX);
                    if (letterBeam != null && letterBeam.enabled)
                        BendPickup(letterBeam.transform, letterLaneX + settings.LetterBeamBaseOffset.x, letterPickup.transform.localPosition.z);
                }
                refreshDepthBend = depthBendStrength != 0f;
            }
            if (gemFlash == null || !gemFlash.enabled) return;
            float progress = (Time.unscaledTime - gemFlashStarted) / activeFlash.Duration;
            if (progress >= 1f)
            {
                gemFlash.enabled = false;
                return;
            }
            float peak = activeFlash.GrowthFraction;
            float size = activeFlash.Size;
            float scale = Mathf.SmoothStep(activeFlash.StartScale * size, size, progress / peak);
            float maxOpacity = activeFlash.Opacity;
            float opacity = progress < peak
                ? Mathf.SmoothStep(0f, maxOpacity, progress / peak)
                : Mathf.SmoothStep(maxOpacity, 0f, (progress - peak) / (1f - peak));
            gemFlash.color = new Color(1f, 1f, 1f, opacity);
            gemFlash.transform.localScale = Vector3.one * scale;
            gemFlash.transform.rotation = worldCamera.transform.rotation * gemFlashRoll;
        }

        private void BendPickup(Transform visual, float laneX, float? referenceZ = null)
        {
            Vector3 position = visual.localPosition;
            float distance = Mathf.Max(0f, (referenceZ ?? position.z) - depthBendStart);
            position.x = laneX + depthBendStrength * distance * distance;
            visual.localPosition = position;
        }
        private static void Hide(TrackItem item)
        {
            item.Active = false;
            item.View.gameObject.SetActive(false);
        }
        private void SetFieldOfView(float reference)
        {
            referenceFieldOfView = reference;
            float scale = Mathf.Max(1, (9f / 16f) / Mathf.Max(.1f, worldCamera.aspect));
            worldCamera.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(reference * Mathf.Deg2Rad * .5f) * scale) * Mathf.Rad2Deg;
        }
    }
}
