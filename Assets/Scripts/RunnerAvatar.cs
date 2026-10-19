using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PillePille
{
    // Cosmetic geometry only. The unchanged PlayerMotor CharacterController owns all collision.
    public sealed class RunnerAvatar : MonoBehaviour
    {
        readonly List<Material> ownedMaterials = new List<Material>();
        Material suit, trim, dark, glow, marker, restraint, shadow, badge;
        Transform bodyRoot, leftArm, rightArm, leftElbow, rightElbow;
        Transform leftLeg, rightLeg, leftKnee, rightKnee, leftEye, rightEye;
        Transform localRing, trapRing, chestHorizontal, chestVertical;
        float stride, movement, measuredSpeed;
        int identity;

        public static RunnerAvatar Create(Transform parent, int id)
        {
            var root = new GameObject("Corredor_Estilo_Deportivo");
            root.transform.SetParent(parent, false);
            var avatar = root.AddComponent<RunnerAvatar>();
            avatar.identity = id;
            avatar.Build();
            return avatar;
        }

        Material MakeMaterial(Color color, float smoothness = .3f, bool emissive = false)
        {
            var material = ArenaBuilder.Material(color);
            material.SetFloat("_Glossiness", smoothness);
            if (emissive) {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * .65f);
            }
            ownedMaterials.Add(material);
            return material;
        }

        Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.layer = 2; // Ignore Raycast; presentation can never obstruct bot or rescue visibility.
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            // Destroy is deferred in Unity. Disable immediately, including on the construction frame.
            var collision = go.GetComponent<Collider>();
            if (collision != null) { collision.enabled = false; Destroy(collision); }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            return go.transform;
        }

        void Build()
        {
            suit = MakeMaterial(new Color(.12f, .78f, .78f), .4f);
            trim = MakeMaterial(new Color(.88f, .94f, .94f), .3f);
            badge = MakeMaterial(new Color(.88f, .94f, .94f), .3f);
            dark = MakeMaterial(new Color(.035f, .065f, .09f), .55f);
            glow = MakeMaterial(new Color(.5f, 1f, .94f), .6f, true);
            marker = MakeMaterial(new Color(.62f, 1f, .92f), .1f, true);
            restraint = MakeMaterial(new Color(1f, .66f, .13f), .3f, true);
            shadow = new Material(Shader.Find("Sprites/Default"));
            shadow.color = new Color(.015f, .025f, .035f, .2f);
            ownedMaterials.Add(shadow);

            bodyRoot = Pivot(transform, "Postura", Vector3.zero);
            Part(bodyRoot, "Chaqueta", PrimitiveType.Sphere, new Vector3(0, .04f, 0), new Vector3(.7f, .7f, .44f), suit);
            Part(bodyRoot, "Cinturon", PrimitiveType.Sphere, new Vector3(0, -.29f, 0), new Vector3(.48f, .22f, .35f), dark);
            // Survivors wear a rescue cross; infected use the same pieces as a warning X.
            chestHorizontal = Part(bodyRoot, "Insignia_A", PrimitiveType.Cube, new Vector3(0, .14f, .211f), new Vector3(.36f, .075f, .046f), badge);
            chestVertical = Part(bodyRoot, "Insignia_B", PrimitiveType.Cube, new Vector3(0, .14f, .213f), new Vector3(.08f, .24f, .048f), badge);
            Part(bodyRoot, "Mochila", PrimitiveType.Sphere, new Vector3(0, .02f, -.245f), new Vector3(.4f, .47f, .22f), dark);
            Part(bodyRoot, "Luz_Mochila", PrimitiveType.Cube, new Vector3(0, .06f, -.359f), new Vector3(.24f, .055f, .025f), glow);
            Part(bodyRoot, "Casco", PrimitiveType.Sphere, new Vector3(0, .61f, 0), new Vector3(.69f, .65f, .64f), suit);
            Part(bodyRoot, "Visor", PrimitiveType.Sphere, new Vector3(0, .62f, .273f), new Vector3(.59f, .29f, .19f), dark);
            leftEye = Part(bodyRoot, "Ojo_Izquierdo", PrimitiveType.Cube, new Vector3(-.135f, .65f, .372f), new Vector3(.155f, .038f, .025f), glow);
            rightEye = Part(bodyRoot, "Ojo_Derecho", PrimitiveType.Cube, new Vector3(.135f, .65f, .372f), new Vector3(.155f, .038f, .025f), glow);
            Part(bodyRoot, "Auricular_Izquierdo", PrimitiveType.Sphere, new Vector3(-.345f, .61f, 0), new Vector3(.09f, .19f, .17f), trim);
            Part(bodyRoot, "Auricular_Derecho", PrimitiveType.Sphere, new Vector3(.345f, .61f, 0), new Vector3(.09f, .19f, .17f), trim);

            BuildArm(-1, out leftArm, out leftElbow);
            BuildArm(1, out rightArm, out rightElbow);
            BuildLeg(-1, out leftLeg, out leftKnee);
            BuildLeg(1, out rightLeg, out rightKnee);

            localRing = Ring(transform, "Indicador_Local", .6f, .038f, marker, -.97f);
            var shadowRing = Ring(transform, "Sombra_Suave", .32f, .31f, shadow, -.978f);
            shadowRing.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            trapRing = Ring(bodyRoot, "Aro_de_Contencion", .43f, .035f, restraint, -.03f);
            trapRing.gameObject.SetActive(false);
            localRing.gameObject.SetActive(false);
        }

        void BuildArm(int side, out Transform shoulder, out Transform elbow)
        {
            string sideName = side < 0 ? "Izquierdo" : "Derecho";
            shoulder = Pivot(bodyRoot, "Hombro_" + sideName, new Vector3(side * .35f, .23f, 0));
            Part(shoulder, "Manga_" + sideName, PrimitiveType.Capsule, new Vector3(0, -.115f, 0), new Vector3(.2f, .145f, .2f), suit);
            elbow = Pivot(shoulder, "Codo_" + sideName, new Vector3(0, -.26f, 0));
            Part(elbow, "Antebrazo_" + sideName, PrimitiveType.Capsule, new Vector3(0, -.1f, 0), new Vector3(.145f, .12f, .145f), trim);
            Part(elbow, "Guante_" + sideName, PrimitiveType.Sphere, new Vector3(0, -.245f, .012f), new Vector3(.17f, .18f, .18f), dark);
        }

        void BuildLeg(int side, out Transform hip, out Transform knee)
        {
            string sideName = side < 0 ? "Izquierda" : "Derecha";
            hip = Pivot(bodyRoot, "Cadera_" + sideName, new Vector3(side * .155f, -.3f, 0));
            Part(hip, "Pantalon_" + sideName, PrimitiveType.Capsule, new Vector3(0, -.135f, 0), new Vector3(.205f, .155f, .205f), dark);
            knee = Pivot(hip, "Rodilla_" + sideName, new Vector3(0, -.275f, 0));
            Part(knee, "Espinillera_" + sideName, PrimitiveType.Capsule, new Vector3(0, -.12f, 0), new Vector3(.155f, .14f, .155f), suit);
            Part(knee, "Zapatilla_" + sideName, PrimitiveType.Cube, new Vector3(0, -.285f, .055f), new Vector3(.215f, .19f, .36f), trim);
        }

        Transform Ring(Transform parent, string name, float radius, float width, Material material, float height)
        {
            const int segments = 40;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++) {
                float angle = (float)i / segments * Mathf.PI * 2;
                var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                vertices[i * 2] = direction * Mathf.Max(0, radius - width);
                vertices[i * 2 + 1] = direction * (radius + width);
                int next = ((i + 1) % segments) * 2;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = i * 2 + 1; triangles[t + 2] = next;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next + 1; triangles[t + 5] = next;
            }
            var ring = Pivot(parent, name, new Vector3(0, height, 0));
            var mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ring.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ring;
        }

        public void Visual(PlayerState state, bool local, Vector3 velocity, bool dashing, float dt)
        {
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            // Authority movement occurs at FixedUpdate frequency. Filter the measured speed so
            // rendering frames between physics steps do not make a running avatar stutter.
            measuredSpeed = Mathf.Lerp(measuredSpeed, speed, 1 - Mathf.Exp(-12 * dt));
            float goal = state.alive && !state.trapped ? Mathf.Clamp01(measuredSpeed / 4f) : 0;
            movement = Mathf.Lerp(movement, goal, 1 - Mathf.Exp(-14 * dt));
            stride += dt * Mathf.Lerp(2, Mathf.Clamp(measuredSpeed * 1.9f, 8, 20), movement);
            float swing = Mathf.Sin(stride) * movement;
            float kneeLeft = Mathf.Max(0, -Mathf.Sin(stride)) * movement * 48;
            float kneeRight = Mathf.Max(0, Mathf.Sin(stride)) * movement * 48;
            float bob = Mathf.Sin(stride * 2) * movement * .025f + Mathf.Sin(Time.time * 2.2f + identity) * (1 - movement) * .012f;
            bodyRoot.localPosition = Vector3.up * bob;
            bodyRoot.localRotation = Quaternion.Euler(state.trapped ? 9 : (dashing ? 17 : 7 * movement), 0, -swing * 2.5f);
            leftLeg.localRotation = Quaternion.Euler(swing * 31, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing * 31, 0, 0);
            leftKnee.localRotation = Quaternion.Euler(-kneeLeft, 0, 0);
            rightKnee.localRotation = Quaternion.Euler(-kneeRight, 0, 0);
            if (state.trapped) {
                leftArm.localRotation = Quaternion.Euler(-38, 0, 36);
                rightArm.localRotation = Quaternion.Euler(-38, 0, -36);
                leftElbow.localRotation = rightElbow.localRotation = Quaternion.Euler(-38, 0, 0);
            } else {
                leftArm.localRotation = Quaternion.Euler(-swing * 38 - movement * 8, 0, 8);
                rightArm.localRotation = Quaternion.Euler(swing * 38 - movement * 8, 0, -8);
                leftElbow.localRotation = Quaternion.Euler(-18 - Mathf.Max(0, swing) * 35, 0, 0);
                rightElbow.localRotation = Quaternion.Euler(-18 - Mathf.Max(0, -swing) * 35, 0, 0);
            }

            var color = !state.alive ? new Color(.34f, .39f, .43f) : state.trapped ? new Color(1, .65f, .15f) : state.tagged ? new Color(1, .27f, .20f) : new Color(.10f, .77f, .78f);
            if (state.safeSeconds > 0 && !state.trapped) color = Color.Lerp(color, Color.white, .16f + Mathf.Sin(Time.time * 10) * .12f);
            suit.color = color;
            bool warning = state.tagged && !state.trapped;
            badge.color = !state.alive ? new Color(.46f, .5f, .53f) : warning ? new Color(1f, .68f, .22f) : state.trapped ? new Color(1f, .92f, .55f) : new Color(.88f, .97f, .96f);
            chestHorizontal.localScale = warning ? new Vector3(.3f, .07f, .046f) : new Vector3(.36f, .075f, .046f);
            chestVertical.localScale = warning ? new Vector3(.3f, .07f, .048f) : new Vector3(.08f, .24f, .048f);
            chestHorizontal.localRotation = Quaternion.Euler(0, 0, warning ? 45 : 0);
            chestVertical.localRotation = Quaternion.Euler(0, 0, warning ? -45 : 0);
            var eyeColor = state.tagged ? new Color(1f, .72f, .35f) : state.trapped ? new Color(1f, .88f, .4f) : new Color(.5f, 1f, .94f);
            glow.color = eyeColor; glow.SetColor("_EmissionColor", eyeColor * .8f);
            leftEye.localRotation = Quaternion.Euler(0, 0, state.tagged ? -16 : 0);
            rightEye.localRotation = Quaternion.Euler(0, 0, state.tagged ? 16 : 0);
            localRing.gameObject.SetActive(local && state.alive);
            marker.color = state.speedSeconds > 0 ? new Color(.66f, 1f, .18f) : state.tagged ? new Color(1f, .48f, .34f) : new Color(.62f, 1f, .92f);
            marker.SetColor("_EmissionColor", marker.color * .7f);
            trapRing.gameObject.SetActive(state.alive && state.trapped);
            if (state.trapped) trapRing.localScale = Vector3.one * (1 + Mathf.Sin(Time.time * 5) * .035f);
        }

        void OnDestroy()
        {
            // The three ring meshes are unique; primitive meshes are Unity-owned and shared.
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.name.EndsWith("_Mesh")) Destroy(filter.sharedMesh);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }
}
