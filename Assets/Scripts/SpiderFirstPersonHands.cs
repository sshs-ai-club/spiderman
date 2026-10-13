using System.Collections.Generic;
using UnityEngine;

namespace SpiderPlayground
{
    // Camera-space glove rig with tapered arm, palm and articulated finger meshes.
    public sealed class SpiderFirstPersonHands
    {
        private static readonly float[] FingerOffsets = { 0.061f, 0.020f, -0.021f, -0.061f };
        private static readonly float[] FingerLengths = { 0.19f, 0.22f, 0.205f, 0.16f };
        private static readonly float[] RestCurls = { 0f, 0f, 0f, 0f };
        private static readonly float[] PalmProfileZ = { -0.145f, -0.105f, -0.025f, 0.07f, 0.125f };
        private static readonly float[] PalmProfileWidth = { 0.067f, 0.087f, 0.106f, 0.112f, 0.087f };
        private static readonly float[] PalmProfileDepth = { 0.041f, 0.051f, 0.055f, 0.046f, 0.033f };

        private sealed class Hand
        {
            public Transform palm;
            public Transform upperArm;
            public Transform forearm;
            public Transform elbowJoint;
            public Transform[] fingerRoots = new Transform[4];
            public Transform[] fingerMiddles = new Transform[4];
            public Transform[] fingerTips = new Transform[4];
            public Transform[] knuckles = new Transform[4];
            public Transform[] middleJoints = new Transform[4];
            public Transform[] distalJoints = new Transform[4];
            public Transform thumbRoot;
            public Transform thumbTip;
            public Transform thumbJoint;
            public Renderer gripWeb;
            public Renderer[] handRenderers;
            public Vector3 rest;
            public float[] curls = new float[4];
            public float thumbCurl;
            public float gripPose;
            public Vector3 gripTopLocal;
            public bool left;
        }

        private readonly Transform cameraRoot;
        private readonly Hand left;
        private readonly Hand right;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly RaycastHit[] wallProbeHits = new RaycastHit[12];
        private readonly Material seamMaterial;
        private readonly Mesh palmMesh;
        private readonly Mesh armMesh;
        private readonly Mesh fingerMesh;
        private readonly Mesh jointMesh;
        private readonly Mesh cuffMesh;
        private bool visible;
        private float climbCycle;

        public SpiderFirstPersonHands(Transform camera, Transform leftPalm,
            Transform rightPalm, Material gloveMaterial, Material webMaterial)
        {
            cameraRoot = camera;
            seamMaterial = new Material(gloveMaterial);
            seamMaterial.name = "First Person Glove Seams (Runtime)";
            seamMaterial.color = new Color(0.025f, 0.035f, 0.055f);
            palmMesh = CreateLoftMesh("Anatomical Glove Palm", true,
                PalmProfileZ, PalmProfileWidth, PalmProfileDepth);
            armMesh = CreateLoftMesh("Tapered Arm", false,
                new[] { -1f, -0.7f, -0.2f, 0.45f, 1f },
                new[] { 0.41f, 0.52f, 0.50f, 0.43f, 0.34f },
                new[] { 0.39f, 0.48f, 0.47f, 0.40f, 0.32f });
            fingerMesh = CreateLoftMesh("Finger Phalanx", false,
                new[] { -1f, -0.66f, 0f, 0.66f, 1f },
                new[] { 0.34f, 0.48f, 0.50f, 0.43f, 0.26f },
                new[] { 0.33f, 0.46f, 0.48f, 0.41f, 0.25f });
            jointMesh = CreateLoftMesh("Soft Glove Joint", false,
                new[] { -1f, -0.65f, 0f, 0.65f, 1f },
                new[] { 0.26f, 0.48f, 0.56f, 0.48f, 0.26f },
                new[] { 0.26f, 0.46f, 0.53f, 0.46f, 0.26f });
            cuffMesh = CreateLoftMesh("Web Shooter Wrist Cuff", true,
                new[] { -0.15f, -0.125f, -0.09f, -0.065f },
                new[] { 0.079f, 0.090f, 0.093f, 0.083f },
                new[] { 0.047f, 0.054f, 0.053f, 0.047f });
            left = BuildHand(leftPalm, true, gloveMaterial, webMaterial);
            right = BuildHand(rightPalm, false, gloveMaterial, webMaterial);
            SetVisible(false);
        }

        private Hand BuildHand(Transform palm, bool isLeft, Material glove,
            Material web)
        {
            string side = isLeft ? "Left" : "Right";
            Vector3 rest = palm.localPosition;
            Transform rigRoot = new GameObject(side + " First Person Hand Rig").transform;
            rigRoot.SetParent(cameraRoot, false);
            rigRoot.localPosition = rest;
            palm.SetParent(rigRoot, false);
            palm.localPosition = Vector3.zero;
            palm.localRotation = Quaternion.identity;
            palm.localScale = Vector3.one;
            palm.GetComponent<MeshFilter>().sharedMesh = palmMesh;

            Hand hand = new Hand
            {
                palm = rigRoot,
                rest = rest,
                left = isLeft
            };
            palm = rigRoot;
            hand.upperArm = CreateMeshPart(side + " First Person Upper Arm",
                cameraRoot, glove, armMesh);
            hand.forearm = CreateMeshPart(side + " First Person Forearm",
                cameraRoot, glove, armMesh);
            hand.elbowJoint = CreateMeshPart(side + " First Person Elbow",
                cameraRoot, glove, jointMesh);

            CreateMeshPart(side + " Glove Cuff", palm, seamMaterial, cuffMesh);
            Transform shooter = CreatePart(side + " Web Shooter",
                PrimitiveType.Cube, palm, seamMaterial);
            shooter.localPosition = new Vector3(0f, 0.065f, -0.105f);
            shooter.localScale = new Vector3(0.065f, 0.018f, 0.065f);
            Transform shooterLens = CreatePart(side + " Web Shooter Lens",
                PrimitiveType.Sphere, palm, web);
            shooterLens.localPosition = new Vector3(0f, 0.081f, -0.095f);
            shooterLens.localScale = new Vector3(0.035f, 0.012f, 0.035f);

            string[] names = { "Index", "Middle", "Ring", "Little" };
            for (int i = 0; i < 4; i++)
            {
                hand.knuckles[i] = CreateMeshPart(side + " " + names[i] + " Knuckle",
                    palm, glove, jointMesh);
                hand.fingerRoots[i] = CreateMeshPart(side + " " + names[i] + " Proximal",
                    palm, glove, fingerMesh);
                hand.fingerMiddles[i] = CreateMeshPart(side + " " + names[i] + " Middle",
                    palm, glove, fingerMesh);
                hand.fingerTips[i] = CreateMeshPart(side + " " + names[i] + " Distal",
                    palm, glove, fingerMesh);
                hand.middleJoints[i] = CreateMeshPart(side + " " + names[i] + " Middle Joint",
                    palm, glove, jointMesh);
                hand.distalJoints[i] = CreateMeshPart(side + " " + names[i] + " Distal Joint",
                    palm, glove, jointMesh);
            }
            hand.thumbRoot = CreateMeshPart(side + " Thumb Proximal",
                palm, glove, fingerMesh);
            hand.thumbTip = CreateMeshPart(side + " Thumb Distal",
                palm, glove, fingerMesh);
            hand.thumbJoint = CreateMeshPart(side + " Thumb Joint",
                palm, glove, jointMesh);

            Transform grip = CreatePart(side + " Held White Web",
                PrimitiveType.Capsule, palm, web);
            // The strand runs vertically through the fist during the yank.
            PositionBone(grip, new Vector3(0f, -0.078f, -0.30f),
                new Vector3(0f, -0.078f, 0.43f), 0.045f);
            hand.gripWeb = grip.GetComponent<Renderer>();
            hand.gripTopLocal = new Vector3(0f, -0.078f, 0.43f);
            hand.gripWeb.enabled = false;
            hand.handRenderers = rigRoot.GetComponentsInChildren<Renderer>();
            UpdateFingers(hand);
            return hand;
        }

        private Transform CreatePart(string name, PrimitiveType shape,
            Transform parent, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            return part.transform;
        }

        private Transform CreateMeshPart(string name, Transform parent,
            Material material, Mesh mesh)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            return part.transform;
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            foreach (Renderer renderer in renderers)
                if (renderer != left.gripWeb && renderer != right.gripWeb)
                    renderer.enabled = isVisible;
            left.gripWeb.enabled = false;
            right.gripWeb.enabled = false;
        }

        public void Animate(SpiderMotionState state, bool leftAttached,
            bool rightAttached, float shotProgress, bool shotLeft,
            float yankProgress, bool yankLeft, Vector3 wallPoint,
            Vector3 wallNormal, Vector3 worldVelocity)
        {
            if (state == SpiderMotionState.WallClimbing)
            {
                Vector3 wallRight = Vector3.Cross(wallNormal, Vector3.up).normalized;
                float climbSpeed = Mathf.Abs(worldVelocity.y) +
                    Mathf.Abs(Vector3.Dot(worldVelocity, wallRight)) * 0.65f;
                if (climbSpeed > 0.15f)
                    climbCycle += climbSpeed * Time.deltaTime * 2.3f;
            }
            AnimateHand(left, state, leftAttached, shotLeft ? shotProgress : -1f,
                yankLeft ? yankProgress : -1f, wallPoint, wallNormal, worldVelocity);
            AnimateHand(right, state, rightAttached, shotLeft ? -1f : shotProgress,
                yankLeft ? -1f : yankProgress, wallPoint, wallNormal, worldVelocity);
        }

        private void AnimateHand(Hand hand, SpiderMotionState state, bool attached,
            float shotProgress, float yankProgress, Vector3 wallPoint,
            Vector3 wallNormal, Vector3 worldVelocity)
        {
            float side = hand.left ? -1f : 1f;
            Vector3 target = hand.rest;
            float phase = Mathf.Sin(Time.time * 8f);
            float wallStroke = 0f;
            switch (state)
            {
                case SpiderMotionState.Walking:
                    target += new Vector3(0f, phase * 0.025f * side, phase * 0.045f * side);
                    break;
                case SpiderMotionState.Jumping:
                    target += new Vector3(side * 0.10f, 0.10f, 0.08f);
                    break;
                case SpiderMotionState.Airborne:
                    target += new Vector3(side * 0.05f, 0.045f, 0.06f);
                    break;
                case SpiderMotionState.WebHanging:
                    float sideSpeed = Vector3.Dot(worldVelocity, cameraRoot.right);
                    float sway = Mathf.Clamp(sideSpeed / 35f, -1f, 1f);
                    target += attached ? new Vector3(-sway * 0.06f, 0.14f, 0.20f) :
                        new Vector3(side * 0.03f - sway * 0.10f, 0.02f, 0.04f);
                    break;
                case SpiderMotionState.WallClimbing:
                    wallStroke = Mathf.Sin(climbCycle + (hand.left ? 0f : Mathf.PI));
                    target = new Vector3(side * 0.34f,
                        -0.10f + wallStroke * 0.11f, 0.45f -
                        Mathf.Max(0f, wallStroke) * 0.035f);
                    break;
            }

            float thumbTarget = 0f;
            float palmPitch = -8f;
            float palmYaw = side * -12f;
            float palmRoll = 0f;
            float shotWeight = 0f;
            if (state == SpiderMotionState.WallClimbing)
            {
                // One hand plants while the other reaches for its next hold.
                thumbTarget = 0.75f;
                palmPitch = -34f;
                palmRoll = side * 14f + wallStroke * side * 7f;
            }
            if (shotProgress >= 0f)
            {
                float progress = Mathf.Clamp01(shotProgress);
                shotWeight = Mathf.SmoothStep(0f, 1f, Mathf.Min(progress * 5f,
                    (1f - progress) * 5f));
                target += new Vector3(side * 0.04f, 0.15f, 0.25f) * shotWeight;
                // Keep the fingers upright for a readable shooting gesture,
                // then turn the palm face away with a full half-turn.
                thumbTarget = Mathf.Lerp(thumbTarget, 0.25f, shotWeight);
                palmPitch = Mathf.Lerp(palmPitch, -105f, shotWeight);
                palmYaw = Mathf.Lerp(palmYaw, 0f, shotWeight);
                palmRoll = Mathf.Lerp(palmRoll, side * 180f, shotWeight);
            }
            if (yankProgress >= 0f)
            {
                float snap = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((yankProgress - 0.18f) / 0.58f));
                target += Vector3.Lerp(new Vector3(0f, 0.18f, 0.35f),
                    new Vector3(side * -0.06f, 0.03f, -0.17f), snap);
                thumbTarget = 1f;
                palmPitch = -90f;
                palmYaw = side * -38f;
                palmRoll = side * 12f;
            }

            float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
            hand.palm.localPosition = Vector3.Lerp(hand.palm.localPosition, target, blend);
            Quaternion targetRotation = Quaternion.Euler(palmPitch, palmYaw, palmRoll);
            if (yankProgress >= 0f)
                targetRotation = Quaternion.AngleAxis(-90f, Vector3.forward) *
                    targetRotation;
            hand.palm.localRotation = Quaternion.Slerp(hand.palm.localRotation,
                targetRotation, blend);
            for (int i = 0; i < 4; i++)
            {
                float targetCurl = state == SpiderMotionState.WallClimbing ?
                    Mathf.Lerp(0.46f, 0.88f, (1f - wallStroke) * 0.5f) : RestCurls[i];
                if (shotProgress >= 0f)
                    targetCurl = Mathf.Lerp(targetCurl,
                        i == 1 || i == 2 ? 1f : 0.03f, shotWeight);
                if (yankProgress >= 0f) targetCurl = 1f;
                hand.curls[i] = Mathf.Lerp(hand.curls[i], targetCurl, blend);
            }
            hand.thumbCurl = Mathf.Lerp(hand.thumbCurl, thumbTarget, blend);
            hand.gripPose = Mathf.Lerp(hand.gripPose,
                yankProgress >= 0f ? 1f : 0f, blend);
            UpdateFingers(hand);
            if (yankProgress >= 0f)
            {
                // The fist turns in the camera view, while the strand remains
                // upright through the closed fingers.
                Vector3 vertical = hand.palm.InverseTransformDirection(
                    cameraRoot.up).normalized;
                Vector3 gripCenter = new Vector3(0f, -0.078f, 0f) +
                    hand.palm.InverseTransformDirection(cameraRoot.right) * 0.055f;
                hand.gripTopLocal = gripCenter + vertical * 0.40f;
                PositionBone(hand.gripWeb.transform,
                    gripCenter - vertical * 0.30f,
                    hand.gripTopLocal, 0.045f);
            }
            hand.gripWeb.enabled = visible && yankProgress >= 0f;
            bool nearWall = state == SpiderMotionState.WallClimbing &&
                wallNormal.sqrMagnitude > 0.5f;
            if (!nearWall)
                nearWall = FindWallNearHand(hand, out wallPoint, out wallNormal);
            if (nearWall) ClampHandToWall(hand, wallPoint, wallNormal);
            UpdateArm(hand, nearWall, wallPoint, wallNormal);
        }

        private bool FindWallNearHand(Hand hand, out Vector3 point,
            out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.zero;
            Vector3 direction = hand.palm.position - cameraRoot.position;
            if (direction.sqrMagnitude < 0.01f) return false;
            int count = Physics.RaycastNonAlloc(cameraRoot.position,
                direction.normalized, wallProbeHits, 1.4f,
                ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = wallProbeHits[i];
                if (hit.collider.GetComponentInParent<WebAttachSurface>() == null ||
                    Mathf.Abs(hit.normal.y) > 0.4f || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                point = hit.point;
                normal = hit.normal;
            }
            return nearest < float.PositiveInfinity;
        }

        private void ClampHandToWall(Hand hand, Vector3 wallPoint,
            Vector3 wallNormal)
        {
            float correction = 0f;
            foreach (Renderer renderer in hand.handRenderers)
            {
                if (!renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                Vector3 extent = bounds.extents;
                float radius = Mathf.Abs(wallNormal.x) * extent.x +
                    Mathf.Abs(wallNormal.y) * extent.y +
                    Mathf.Abs(wallNormal.z) * extent.z;
                float clearance = Vector3.Dot(bounds.center - wallPoint,
                    wallNormal) - radius;
                correction = Mathf.Max(correction, 0.018f - clearance);
            }
            if (correction > 0f)
                hand.palm.position += wallNormal * correction;
        }

        private Vector3 KeepArmPointOutsideWall(Vector3 cameraLocalPoint,
            Vector3 wallPoint, Vector3 wallNormal)
        {
            Vector3 worldPoint = cameraRoot.TransformPoint(cameraLocalPoint);
            float clearance = Vector3.Dot(worldPoint - wallPoint, wallNormal);
            if (clearance < 0.12f)
                worldPoint += wallNormal * (0.12f - clearance);
            return cameraRoot.InverseTransformPoint(worldPoint);
        }

        private void UpdateArm(Hand hand, bool againstWall,
            Vector3 wallPoint, Vector3 wallNormal)
        {
            float side = hand.left ? -1f : 1f;
            Vector3 upper = new Vector3(side * 0.58f, -0.98f, 0.08f);
            Vector3 elbow = new Vector3(side * 0.49f, -0.65f, 0.32f);
            Vector3 wrist = hand.palm.localPosition +
                hand.palm.localRotation * new Vector3(0f, 0f, -0.11f);
            if (againstWall && wallNormal.sqrMagnitude > 0.5f)
            {
                upper = KeepArmPointOutsideWall(upper, wallPoint, wallNormal);
                elbow = KeepArmPointOutsideWall(elbow, wallPoint, wallNormal);
                wrist = KeepArmPointOutsideWall(wrist, wallPoint, wallNormal);
            }
            PositionBone(hand.upperArm, upper, elbow, 0.18f);
            PositionBone(hand.forearm, elbow, wrist, 0.145f);
            hand.elbowJoint.localPosition = elbow;
            hand.elbowJoint.localScale = Vector3.one * 0.15f;
        }

        public Vector3 GetWebOrigin(bool isLeft, bool gripping)
        {
            Hand hand = isLeft ? left : right;
            return hand.palm.TransformPoint(gripping ?
                hand.gripTopLocal :
                new Vector3(0f, -0.055f, 0.17f));
        }

        private static void UpdateFingers(Hand hand)
        {
            float inward = hand.left ? 1f : -1f;
            for (int i = 0; i < 4; i++)
            {
                float x = inward * FingerOffsets[i];
                Vector3 root = new Vector3(x, 0f, 0.105f);
                float length = FingerLengths[i];
                Vector3 openFirst = root + Vector3.forward * (length * 0.43f);
                Vector3 openSecond = root + Vector3.forward * (length * 0.76f);
                Vector3 openTip = root + Vector3.forward * FingerLengths[i];
                Vector3 shotFirst = root + new Vector3(0f, -0.028f, 0.070f);
                Vector3 shotSecond = root + new Vector3(0f, -0.062f, -0.005f);
                Vector3 shotTip = new Vector3(x, -0.055f, 0.018f);
                Vector3 gripFirst = root + new Vector3(0f, -0.044f, 0.045f);
                Vector3 gripSecond = root + new Vector3(0f, -0.108f, -0.015f);
                Vector3 gripTip = new Vector3(x, -0.088f, -0.068f);
                Vector3 first = Vector3.Lerp(openFirst,
                    Vector3.Lerp(shotFirst, gripFirst, hand.gripPose), hand.curls[i]);
                Vector3 second = Vector3.Lerp(openSecond,
                    Vector3.Lerp(shotSecond, gripSecond, hand.gripPose), hand.curls[i]);
                Vector3 tip = Vector3.Lerp(openTip,
                    Vector3.Lerp(shotTip, gripTip, hand.gripPose), hand.curls[i]);
                float radius = i == 3 ? 0.038f : 0.044f;
                PositionBone(hand.fingerRoots[i], root, first, radius);
                PositionBone(hand.fingerMiddles[i], first, second, radius * 0.90f);
                PositionBone(hand.fingerTips[i], second, tip, radius * 0.77f);
                hand.knuckles[i].localPosition = root;
                hand.knuckles[i].localScale = Vector3.one * radius * 0.76f;
                hand.middleJoints[i].localPosition = first;
                hand.middleJoints[i].localScale = Vector3.one * radius * 0.70f;
                hand.distalJoints[i].localPosition = second;
                hand.distalJoints[i].localScale = Vector3.one * radius * 0.62f;
            }

            Vector3 thumbBase = new Vector3(inward * 0.10f, -0.015f, -0.025f);
            Vector3 thumbJoint = Vector3.Lerp(
                new Vector3(inward * 0.17f, -0.025f, 0.07f),
                new Vector3(inward * 0.135f, -0.055f, 0.07f), hand.thumbCurl);
            Vector3 thumbTip = Vector3.Lerp(
                new Vector3(inward * 0.17f, -0.025f, 0.15f),
                Vector3.Lerp(new Vector3(inward * 0.02f, -0.055f, 0.08f),
                    new Vector3(inward * 0.015f, -0.090f, 0.030f), hand.gripPose),
                hand.thumbCurl);
            PositionBone(hand.thumbRoot, thumbBase, thumbJoint, 0.052f);
            PositionBone(hand.thumbTip, thumbJoint, thumbTip, 0.043f);
            hand.thumbJoint.localPosition = thumbJoint;
            hand.thumbJoint.localScale = Vector3.one * 0.037f;
        }

        private static Mesh CreateLoftMesh(string name, bool alongZ,
            float[] positions, float[] radiiA, float[] radiiB)
        {
            const int sides = 16;
            int rings = positions.Length;
            Vector3[] vertices = new Vector3[rings * sides + 2];
            int[] triangles = new int[(rings - 1) * sides * 6 + sides * 6];
            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    float a = Mathf.Cos(angle) * radiiA[ring];
                    float b = Mathf.Sin(angle) * radiiB[ring];
                    vertices[ring * sides + side] = alongZ
                        ? new Vector3(a, b, positions[ring])
                        : new Vector3(a, positions[ring], b);
                }
            }
            vertices[rings * sides] = alongZ
                ? new Vector3(0f, 0f, positions[0])
                : new Vector3(0f, positions[0], 0f);
            vertices[rings * sides + 1] = alongZ
                ? new Vector3(0f, 0f, positions[rings - 1])
                : new Vector3(0f, positions[rings - 1], 0f);

            int index = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int a = ring * sides + side;
                    int b = ring * sides + (side + 1) % sides;
                    int c = (ring + 1) * sides + side;
                    int d = (ring + 1) * sides + (side + 1) % sides;
                    if (alongZ)
                    {
                        triangles[index++] = a; triangles[index++] = b; triangles[index++] = c;
                        triangles[index++] = b; triangles[index++] = d; triangles[index++] = c;
                    }
                    else
                    {
                        triangles[index++] = a; triangles[index++] = c; triangles[index++] = b;
                        triangles[index++] = b; triangles[index++] = c; triangles[index++] = d;
                    }
                }
            }
            int startCenter = rings * sides;
            int endCenter = startCenter + 1;
            int endRing = (rings - 1) * sides;
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles[index++] = startCenter;
                triangles[index++] = alongZ ? next : side;
                triangles[index++] = alongZ ? side : next;
                triangles[index++] = endCenter;
                triangles[index++] = endRing + (alongZ ? side : next);
                triangles[index++] = endRing + (alongZ ? next : side);
            }
            Mesh mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void PositionBone(Transform bone, Vector3 from,
            Vector3 to, float width)
        {
            Vector3 axis = to - from;
            bone.localPosition = (from + to) * 0.5f;
            bone.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            bone.localScale = new Vector3(width, axis.magnitude * 0.5f, width);
        }

        public void Dispose()
        {
            if (seamMaterial != null) Object.Destroy(seamMaterial);
            Object.Destroy(palmMesh);
            Object.Destroy(armMesh);
            Object.Destroy(fingerMesh);
            Object.Destroy(jointMesh);
            Object.Destroy(cuffMesh);
        }
    }
}
