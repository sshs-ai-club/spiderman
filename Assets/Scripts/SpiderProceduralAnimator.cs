using System.Collections.Generic;
using UnityEngine;

namespace SpiderPlayground
{
    public enum SpiderMotionState
    {
        Idle,
        Walking,
        Jumping,
        Airborne,
        WebHanging,
        WallClimbing
    }

    // Animates a small two-segment rig without an imported model or animation assets.
    // The existing scene stays usable: the rig is built from its original limb materials at runtime.
    public sealed class SpiderProceduralAnimator : MonoBehaviour
    {
        private struct Pose
        {
            public Vector3 leftHand, rightHand, leftElbow, rightElbow;
            public Vector3 leftFoot, rightFoot, leftKnee, rightKnee;
            public float bob, lean, headTilt;
        }

        private Transform appearance;
        private Transform torso, hips, head, chest, neck, leftHand, rightHand;
        private Renderer[] oldLimbs;
        private readonly List<Renderer> generatedRenderers = new List<Renderer>();
        private Material whiteMaterial, blackMaterial;
        private Transform leftUpperArm, leftForearm, rightUpperArm, rightForearm;
        private Transform leftThigh, leftShin, rightThigh, rightShin;
        private Transform leftFoot, rightFoot;
        private Transform leftWristTrim, rightWristTrim, leftBoot, rightBoot;
        private Transform frontEmblem, backEmblem, leftSidePanel, rightSidePanel;
        private Transform leftShoulderJoint, rightShoulderJoint, leftElbowJoint, rightElbowJoint;
        private Transform leftHipJoint, rightHipJoint, leftKneeJoint, rightKneeJoint;
        private Vector3 torsoHome, hipsHome, headHome, chestHome, neckHome;
        private Vector3 leftElbowPosition, rightElbowPosition;
        private Vector3 leftKneePosition, rightKneePosition;
        private Vector3 leftFootPosition, rightFootPosition;
        private float walkCycle, climbCycle;
        private enum WebAction { None, Shot, Yank }
        private WebAction webAction;
        private bool webActionLeft;
        private float webActionStart, webActionEnd;
        private bool ready, firstPose = true;

        public void PlayWebShot(bool fromLeft)
        {
            webAction = WebAction.Shot;
            webActionLeft = fromLeft;
            webActionStart = Time.time;
            webActionEnd = Time.time + 0.28f;
        }

        public void PlayWebYank(bool fromLeft)
        {
            webAction = WebAction.Yank;
            webActionLeft = fromLeft;
            webActionStart = Time.time;
            webActionEnd = Time.time + 0.42f;
        }

        public void Initialize(Transform bodyAppearance, Transform left, Transform right)
        {
            if (ready || bodyAppearance == null || left == null || right == null) return;
            appearance = bodyAppearance;
            torso = appearance.Find("Torso");
            hips = appearance.Find("Hips");
            head = appearance.Find("Faceless Head");
            Transform oldLeftArm = appearance.Find("Left Arm");
            Transform oldRightArm = appearance.Find("Right Arm");
            Transform oldLeftLeg = appearance.Find("Left Leg");
            Transform oldRightLeg = appearance.Find("Right Leg");
            if (torso == null || hips == null || head == null ||
                oldLeftArm == null || oldRightArm == null ||
                oldLeftLeg == null || oldRightLeg == null) return;

            leftHand = left;
            rightHand = right;
            torso.localPosition = new Vector3(0f, 1.31f, 0f);
            torso.localScale = new Vector3(0.55f, 0.38f, 0.32f);
            hips.localPosition = new Vector3(0f, 0.94f, 0f);
            hips.localScale = new Vector3(0.47f, 0.25f, 0.32f);
            head.localPosition = new Vector3(0f, 1.82f, 0f);
            head.localScale = new Vector3(0.27f, 0.32f, 0.28f);
            leftHand.localScale = new Vector3(0.15f, 0.16f, 0.17f);
            rightHand.localScale = leftHand.localScale;
            torsoHome = torso.localPosition;
            hipsHome = hips.localPosition;
            headHome = head.localPosition;
            oldLimbs = new[]
            {
                oldLeftArm.GetComponent<Renderer>(), oldRightArm.GetComponent<Renderer>(),
                oldLeftLeg.GetComponent<Renderer>(), oldRightLeg.GetComponent<Renderer>()
            };
            Material armMaterial = oldLimbs[0].sharedMaterial;
            Material legMaterial = oldLimbs[2].sharedMaterial;
            whiteMaterial = new Material(armMaterial);
            whiteMaterial.name = "Spider Eye White (Runtime)";
            whiteMaterial.color = new Color(0.94f, 0.96f, 1f);
            blackMaterial = new Material(armMaterial);
            blackMaterial.name = "Spider Web Black (Runtime)";
            blackMaterial.color = new Color(0.035f, 0.045f, 0.065f);

            chest = MakePart("Animated Shoulder Bridge", PrimitiveType.Sphere, armMaterial);
            chestHome = new Vector3(0f, 1.54f, 0f);
            chest.localPosition = chestHome;
            chest.localScale = new Vector3(0.57f, 0.24f, 0.34f);
            neck = MakePart("Animated Neck", PrimitiveType.Sphere, armMaterial);
            neckHome = new Vector3(0f, 1.69f, 0f);
            neck.localPosition = neckHome;
            neck.localScale = new Vector3(0.15f, 0.18f, 0.15f);

            leftUpperArm = MakePart("Animated Left Upper Arm", PrimitiveType.Capsule, armMaterial);
            leftForearm = MakePart("Animated Left Forearm", PrimitiveType.Capsule, armMaterial);
            rightUpperArm = MakePart("Animated Right Upper Arm", PrimitiveType.Capsule, armMaterial);
            rightForearm = MakePart("Animated Right Forearm", PrimitiveType.Capsule, armMaterial);
            leftThigh = MakePart("Animated Left Thigh", PrimitiveType.Capsule, legMaterial);
            leftShin = MakePart("Animated Left Shin", PrimitiveType.Capsule, legMaterial);
            rightThigh = MakePart("Animated Right Thigh", PrimitiveType.Capsule, legMaterial);
            rightShin = MakePart("Animated Right Shin", PrimitiveType.Capsule, legMaterial);
            leftFoot = MakePart("Animated Left Foot", PrimitiveType.Sphere, armMaterial);
            rightFoot = MakePart("Animated Right Foot", PrimitiveType.Sphere, armMaterial);
            leftShoulderJoint = MakePart("Animated Left Shoulder", PrimitiveType.Sphere, armMaterial);
            rightShoulderJoint = MakePart("Animated Right Shoulder", PrimitiveType.Sphere, armMaterial);
            leftElbowJoint = MakePart("Animated Left Elbow", PrimitiveType.Sphere, armMaterial);
            rightElbowJoint = MakePart("Animated Right Elbow", PrimitiveType.Sphere, armMaterial);
            leftHipJoint = MakePart("Animated Left Hip", PrimitiveType.Sphere, legMaterial);
            rightHipJoint = MakePart("Animated Right Hip", PrimitiveType.Sphere, legMaterial);
            leftKneeJoint = MakePart("Animated Left Knee", PrimitiveType.Sphere, legMaterial);
            rightKneeJoint = MakePart("Animated Right Knee", PrimitiveType.Sphere, legMaterial);
            leftWristTrim = MakePart("Left Web Shooter Band", PrimitiveType.Sphere, blackMaterial);
            rightWristTrim = MakePart("Right Web Shooter Band", PrimitiveType.Sphere, blackMaterial);
            leftBoot = MakePart("Left Red Boot", PrimitiveType.Capsule, armMaterial);
            rightBoot = MakePart("Right Red Boot", PrimitiveType.Capsule, armMaterial);
            frontEmblem = CreateSpiderEmblem("Front Spider Emblem", 0f);
            backEmblem = CreateSpiderEmblem("Back Spider Emblem", 180f);
            leftSidePanel = MakePart("Left Blue Suit Panel", PrimitiveType.Sphere, legMaterial);
            rightSidePanel = MakePart("Right Blue Suit Panel", PrimitiveType.Sphere, legMaterial);
            leftSidePanel.localScale = new Vector3(0.07f, 0.40f, 0.06f);
            rightSidePanel.localScale = leftSidePanel.localScale;
            CreateMaskEyes();
            ready = true;
            SetThirdPersonVisible(true);
            Animate(SpiderMotionState.Idle, Vector3.zero,
                false, Vector3.zero, false, Vector3.zero);
        }

        private Transform MakePart(string partName, PrimitiveType shape, Material material,
            Transform parent = null)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(parent == null ? appearance : parent, false);
            Renderer partRenderer = part.GetComponent<Renderer>();
            partRenderer.sharedMaterial = material;
            generatedRenderers.Add(partRenderer);
            Collider partCollider = part.GetComponent<Collider>();
            partCollider.enabled = false;
            Destroy(partCollider);
            return part.transform;
        }

        private Transform CreateSpiderEmblem(string name, float yaw)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(appearance, false);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            AddDecoration(root, "Spider Body", blackMaterial,
                new Vector3(0f, 0f, 0.02f), new Vector3(0.11f, 0.17f, 0.045f));
            AddDecoration(root, "Spider Head", blackMaterial,
                new Vector3(0f, 0.12f, 0.025f), new Vector3(0.075f, 0.075f, 0.05f));
            for (int side = -1; side <= 1; side += 2)
            {
                for (int leg = 0; leg < 4; leg++)
                {
                    Vector3 start = new Vector3(side * 0.04f, 0.09f - leg * 0.054f, 0.025f);
                    Vector3 elbow = new Vector3(side * (0.12f + leg * 0.009f),
                        0.18f - leg * 0.10f, 0.025f);
                    Vector3 tip = new Vector3(side * (0.22f + leg * 0.004f),
                        0.17f - leg * 0.13f, 0.025f);
                    AddEmblemLeg(root, start, elbow);
                    AddEmblemLeg(root, elbow, tip);
                }
            }
            return root;
        }

        private void AddEmblemLeg(Transform parent, Vector3 from, Vector3 to)
        {
            Transform leg = MakePart("Spider Emblem Leg", PrimitiveType.Capsule,
                blackMaterial, parent);
            PositionBone(leg, from, to, 0.018f);
        }

        private void CreateMaskEyes()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Transform patch = AddDecoration(head,
                    side < 0 ? "Left Black Eye Frame" : "Right Black Eye Frame",
                    blackMaterial, new Vector3(side * 0.235f, 0.07f, 0.43f),
                    new Vector3(0.32f, 0.43f, 0.09f));
                patch.localRotation = Quaternion.Euler(0f, side * 12f, side * 23f);
                Transform lens = AddDecoration(head,
                    side < 0 ? "Left White Eye Lens" : "Right White Eye Lens",
                    whiteMaterial, new Vector3(side * 0.235f, 0.07f, 0.49f),
                    new Vector3(0.245f, 0.35f, 0.06f));
                lens.localRotation = patch.localRotation;
            }
            AddDecoration(head, "Mask Center Web Seam", blackMaterial,
                new Vector3(0f, 0f, 0.505f), new Vector3(0.018f, 0.72f, 0.018f));
            AddMaskLine(new Vector3(-0.34f, 0.25f, 0.36f),
                new Vector3(0f, 0.30f, 0.41f));
            AddMaskLine(new Vector3(0f, 0.30f, 0.41f),
                new Vector3(0.34f, 0.25f, 0.36f));
            AddMaskLine(new Vector3(-0.34f, -0.18f, 0.37f),
                new Vector3(0f, -0.22f, 0.44f));
            AddMaskLine(new Vector3(0f, -0.22f, 0.44f),
                new Vector3(0.34f, -0.18f, 0.37f));
        }

        private void AddMaskLine(Vector3 from, Vector3 to)
        {
            Transform line = MakePart("Mask Web Seam", PrimitiveType.Capsule,
                blackMaterial, head);
            PositionBone(line, from, to, 0.024f);
        }

        private Transform AddDecoration(Transform parent, string name, Material material,
            Vector3 position, Vector3 scale)
        {
            Transform part = MakePart(name, PrimitiveType.Sphere, material, parent);
            part.localPosition = position;
            part.localScale = scale;
            return part;
        }

        public void SetThirdPersonVisible(bool visible)
        {
            if (!ready) return;
            foreach (Renderer renderer in oldLimbs)
                renderer.enabled = false;
            foreach (Renderer renderer in generatedRenderers)
                renderer.enabled = visible;
        }

        private void OnDestroy()
        {
            if (whiteMaterial != null) Destroy(whiteMaterial);
            if (blackMaterial != null) Destroy(blackMaterial);
        }

        public void Animate(SpiderMotionState state, Vector3 worldVelocity,
            bool leftWebAttached, Vector3 leftAnchor, bool rightWebAttached, Vector3 rightAnchor)
        {
            if (!ready) return;

            float deltaTime = Time.deltaTime;
            Vector3 localVelocity = appearance.InverseTransformDirection(worldVelocity);
            if (state == SpiderMotionState.Walking)
                walkCycle += new Vector2(worldVelocity.x, worldVelocity.z).magnitude * deltaTime * 1.45f;
            if (state == SpiderMotionState.WallClimbing)
                climbCycle += Mathf.Abs(worldVelocity.y) * deltaTime * 2f;

            Pose target = BuildPose(state, localVelocity, leftWebAttached, leftAnchor,
                rightWebAttached, rightAnchor);
            float blend = firstPose ? 1f : 1f - Mathf.Exp(-13f * deltaTime);
            firstPose = false;

            leftHand.localPosition = Vector3.Lerp(leftHand.localPosition, target.leftHand, blend);
            rightHand.localPosition = Vector3.Lerp(rightHand.localPosition, target.rightHand, blend);
            leftElbowPosition = Vector3.Lerp(leftElbowPosition, target.leftElbow, blend);
            rightElbowPosition = Vector3.Lerp(rightElbowPosition, target.rightElbow, blend);
            leftKneePosition = Vector3.Lerp(leftKneePosition, target.leftKnee, blend);
            rightKneePosition = Vector3.Lerp(rightKneePosition, target.rightKnee, blend);
            leftFootPosition = Vector3.Lerp(leftFootPosition, target.leftFoot, blend);
            rightFootPosition = Vector3.Lerp(rightFootPosition, target.rightFoot, blend);

            torso.localPosition = Vector3.Lerp(torso.localPosition,
                torsoHome + Vector3.up * target.bob, blend);
            hips.localPosition = Vector3.Lerp(hips.localPosition,
                hipsHome + Vector3.up * target.bob * 0.65f, blend);
            head.localPosition = Vector3.Lerp(head.localPosition,
                headHome + Vector3.up * target.bob, blend);
            chest.localPosition = Vector3.Lerp(chest.localPosition,
                chestHome + Vector3.up * target.bob, blend);
            neck.localPosition = Vector3.Lerp(neck.localPosition,
                neckHome + Vector3.up * target.bob, blend);
            torso.localRotation = Quaternion.Slerp(torso.localRotation,
                Quaternion.Euler(target.lean, 0f, target.headTilt * 0.35f), blend);
            chest.localRotation = Quaternion.Slerp(chest.localRotation,
                Quaternion.Euler(target.lean * 0.55f, 0f, target.headTilt * 0.35f), blend);
            neck.localRotation = Quaternion.Slerp(neck.localRotation,
                Quaternion.Euler(target.lean * 0.3f, 0f, target.headTilt * 0.35f), blend);
            head.localRotation = Quaternion.Slerp(head.localRotation,
                Quaternion.Euler(-target.lean * 0.45f, 0f, target.headTilt), blend);

            float torsoBob = torso.localPosition.y - torsoHome.y;
            float hipBob = hips.localPosition.y - hipsHome.y;
            frontEmblem.localPosition = new Vector3(0f, 1.37f + torsoBob, 0.175f);
            backEmblem.localPosition = new Vector3(0f, 1.37f + torsoBob, -0.175f);
            frontEmblem.localRotation = torso.localRotation;
            backEmblem.localRotation = torso.localRotation * Quaternion.Euler(0f, 180f, 0f);
            leftSidePanel.localPosition = new Vector3(-0.235f, 1.22f + torsoBob, 0.12f);
            rightSidePanel.localPosition = new Vector3(0.235f, 1.22f + torsoBob, 0.12f);
            leftSidePanel.localRotation = torso.localRotation;
            rightSidePanel.localRotation = torso.localRotation;
            Vector3 leftShoulder = new Vector3(-0.30f, 1.53f + torsoBob, 0.01f);
            Vector3 rightShoulder = new Vector3(0.30f, 1.53f + torsoBob, 0.01f);
            Vector3 leftHip = new Vector3(-0.17f, 0.95f + hipBob, 0f);
            Vector3 rightHip = new Vector3(0.17f, 0.95f + hipBob, 0f);
            PositionBone(leftUpperArm, leftShoulder, leftElbowPosition, 0.15f);
            PositionBone(leftForearm, leftElbowPosition, leftHand.localPosition, 0.13f);
            PositionBone(rightUpperArm, rightShoulder, rightElbowPosition, 0.15f);
            PositionBone(rightForearm, rightElbowPosition, rightHand.localPosition, 0.13f);
            PositionBone(leftThigh, leftHip, leftKneePosition, 0.19f);
            PositionBone(leftShin, leftKneePosition, leftFootPosition, 0.16f);
            PositionBone(rightThigh, rightHip, rightKneePosition, 0.19f);
            PositionBone(rightShin, rightKneePosition, rightFootPosition, 0.16f);
            PositionJoint(leftShoulderJoint, leftShoulder, 0.20f);
            PositionJoint(rightShoulderJoint, rightShoulder, 0.20f);
            PositionJoint(leftElbowJoint, leftElbowPosition, 0.15f);
            PositionJoint(rightElbowJoint, rightElbowPosition, 0.15f);
            PositionJoint(leftHipJoint, leftHip, 0.19f);
            PositionJoint(rightHipJoint, rightHip, 0.19f);
            PositionJoint(leftKneeJoint, leftKneePosition, 0.19f);
            PositionJoint(rightKneeJoint, rightKneePosition, 0.19f);
            PositionFoot(leftFoot, leftFootPosition);
            PositionFoot(rightFoot, rightFootPosition);
            PositionBand(leftWristTrim, leftElbowPosition, leftHand.localPosition,
                0.78f, 0.155f, 0.035f);
            PositionBand(rightWristTrim, rightElbowPosition, rightHand.localPosition,
                0.78f, 0.155f, 0.035f);
            PositionBone(leftBoot, Vector3.Lerp(leftKneePosition, leftFootPosition, 0.60f),
                leftFootPosition, 0.17f);
            PositionBone(rightBoot, Vector3.Lerp(rightKneePosition, rightFootPosition, 0.60f),
                rightFootPosition, 0.17f);
        }

        private Pose BuildPose(SpiderMotionState state, Vector3 velocity,
            bool leftAttached, Vector3 leftAnchor, bool rightAttached, Vector3 rightAnchor)
        {
            Pose pose = new Pose
            {
                leftHand = new Vector3(-0.37f, 0.86f, 0.06f),
                rightHand = new Vector3(0.37f, 0.86f, 0.06f),
                leftElbow = new Vector3(-0.39f, 1.19f, 0.02f),
                rightElbow = new Vector3(0.39f, 1.19f, 0.02f),
                leftFoot = new Vector3(-0.19f, 0.08f, 0.04f),
                rightFoot = new Vector3(0.19f, 0.08f, 0.04f),
                leftKnee = new Vector3(-0.19f, 0.51f, 0.1f),
                rightKnee = new Vector3(0.19f, 0.51f, 0.1f),
                bob = Mathf.Sin(Time.time * 2.1f) * 0.009f
            };

            switch (state)
            {
                case SpiderMotionState.Walking:
                    float leftStep = Mathf.Sin(walkCycle);
                    float rightStep = -leftStep;
                    pose.leftFoot = new Vector3(-0.20f,
                        0.08f + Mathf.Max(0f, leftStep) * 0.17f, leftStep * 0.37f);
                    pose.rightFoot = new Vector3(0.20f,
                        0.08f + Mathf.Max(0f, rightStep) * 0.17f, rightStep * 0.37f);
                    pose.leftKnee = new Vector3(-0.20f, 0.52f + Mathf.Max(0f, leftStep) * 0.10f,
                        pose.leftFoot.z * 0.5f + 0.10f);
                    pose.rightKnee = new Vector3(0.20f, 0.52f + Mathf.Max(0f, rightStep) * 0.10f,
                        pose.rightFoot.z * 0.5f + 0.10f);
                    pose.leftHand = new Vector3(-0.38f, 0.90f + leftStep * 0.04f,
                        0.03f - leftStep * 0.28f);
                    pose.rightHand = new Vector3(0.38f, 0.90f + rightStep * 0.04f,
                        0.03f - rightStep * 0.28f);
                    pose.leftElbow = new Vector3(-0.39f, 1.19f,
                        0.02f - leftStep * 0.13f);
                    pose.rightElbow = new Vector3(0.39f, 1.19f,
                        0.02f - rightStep * 0.13f);
                    pose.bob = Mathf.Abs(Mathf.Sin(walkCycle * 2f)) * 0.035f;
                    pose.lean = -7f;
                    pose.headTilt = Mathf.Sin(walkCycle) * 2.4f;
                    break;

                case SpiderMotionState.Jumping:
                    pose.leftHand = new Vector3(-0.91f, 1.71f, 0.08f);
                    pose.rightHand = new Vector3(0.91f, 1.71f, 0.08f);
                    pose.leftElbow = new Vector3(-0.62f, 1.57f, 0.02f);
                    pose.rightElbow = new Vector3(0.62f, 1.57f, 0.02f);
                    pose.leftFoot = new Vector3(-0.23f, 0.29f, -0.20f);
                    pose.rightFoot = new Vector3(0.23f, 0.29f, -0.20f);
                    pose.leftKnee = new Vector3(-0.22f, 0.62f, 0.16f);
                    pose.rightKnee = new Vector3(0.22f, 0.62f, 0.16f);
                    pose.lean = 10f;
                    break;

                case SpiderMotionState.Airborne:
                    pose.leftHand = new Vector3(-0.78f, 1.39f, -0.08f);
                    pose.rightHand = new Vector3(0.78f, 1.39f, -0.08f);
                    pose.leftElbow = new Vector3(-0.55f, 1.42f, -0.14f);
                    pose.rightElbow = new Vector3(0.55f, 1.42f, -0.14f);
                    pose.leftFoot = new Vector3(-0.24f, 0.19f, -0.28f);
                    pose.rightFoot = new Vector3(0.24f, 0.16f, -0.35f);
                    pose.leftKnee = new Vector3(-0.23f, 0.53f, 0.10f);
                    pose.rightKnee = new Vector3(0.23f, 0.50f, 0.10f);
                    pose.lean = -8f;
                    pose.headTilt = Mathf.Sin(Time.time * 5f) * 2f;
                    break;

                case SpiderMotionState.WebHanging:
                    float lateralSwing = Mathf.Clamp(velocity.x / 32f, -1f, 1f);
                    float swingEnergy = Mathf.Clamp01(
                        new Vector2(velocity.x, velocity.z).magnitude / 34f);
                    pose.leftHand = leftAttached
                        ? ReachTowardAnchor(new Vector3(-0.32f, 1.53f, 0.02f), leftAnchor)
                        : new Vector3(-0.72f - lateralSwing * 0.16f,
                            1.31f, -0.08f - swingEnergy * 0.10f);
                    pose.rightHand = rightAttached
                        ? ReachTowardAnchor(new Vector3(0.32f, 1.53f, 0.02f), rightAnchor)
                        : new Vector3(0.72f - lateralSwing * 0.16f,
                            1.31f, -0.08f - swingEnergy * 0.10f);
                    pose.leftElbow = Vector3.Lerp(new Vector3(-0.32f, 1.53f, 0f), pose.leftHand, 0.48f)
                        + new Vector3(-0.11f, -0.07f, -0.06f);
                    pose.rightElbow = Vector3.Lerp(new Vector3(0.32f, 1.53f, 0f), pose.rightHand, 0.48f)
                        + new Vector3(0.11f, -0.07f, -0.06f);
                    float kick = Mathf.Sin(Time.time * (4.5f + swingEnergy * 3f));
                    pose.leftFoot = new Vector3(-0.25f - lateralSwing * 0.12f,
                        0.27f + kick * (0.08f + swingEnergy * 0.08f),
                        -0.26f - swingEnergy * 0.12f);
                    pose.rightFoot = new Vector3(0.25f - lateralSwing * 0.12f,
                        0.27f - kick * (0.08f + swingEnergy * 0.08f),
                        -0.26f - swingEnergy * 0.12f);
                    pose.leftKnee = new Vector3(-0.23f - lateralSwing * 0.07f,
                        0.58f, 0.14f + swingEnergy * 0.08f);
                    pose.rightKnee = new Vector3(0.23f - lateralSwing * 0.07f,
                        0.58f, 0.14f + swingEnergy * 0.08f);
                    pose.bob = Mathf.Sin(Time.time * 7f) * swingEnergy * 0.025f;
                    pose.lean = Mathf.Clamp(-velocity.z * 1.8f, -18f, 18f);
                    pose.headTilt = leftAttached && !rightAttached ? -7f :
                        rightAttached && !leftAttached ? 7f : 0f;
                    pose.headTilt += lateralSwing * 14f;
                    break;

                case SpiderMotionState.WallClimbing:
                    float leftReach = Mathf.Sin(climbCycle);
                    float rightReach = -leftReach;
                    pose.leftHand = new Vector3(-0.48f, 1.72f + leftReach * 0.21f, 0.46f);
                    pose.rightHand = new Vector3(0.48f, 1.72f + rightReach * 0.21f, 0.46f);
                    pose.leftElbow = new Vector3(-0.48f, 1.34f + leftReach * 0.09f, 0.22f);
                    pose.rightElbow = new Vector3(0.48f, 1.34f + rightReach * 0.09f, 0.22f);
                    pose.leftFoot = new Vector3(-0.22f, 0.25f - leftReach * 0.16f, 0.42f);
                    pose.rightFoot = new Vector3(0.22f, 0.25f - rightReach * 0.16f, 0.42f);
                    pose.leftKnee = new Vector3(-0.23f, 0.58f - leftReach * 0.06f, 0.18f);
                    pose.rightKnee = new Vector3(0.23f, 0.58f - rightReach * 0.06f, 0.18f);
                    pose.bob = Mathf.Sin(climbCycle * 2f) * 0.025f;
                    pose.lean = -10f;
                    pose.headTilt = leftReach * 2.5f;
                    break;
            }
            ApplyWebActionPose(ref pose);
            return pose;
        }

        private void ApplyWebActionPose(ref Pose pose)
        {
            if (webAction == WebAction.None || Time.time >= webActionEnd)
            {
                webAction = WebAction.None;
                return;
            }

            float progress = Mathf.Clamp01((Time.time - webActionStart) /
                (webActionEnd - webActionStart));
            if (webAction == WebAction.Shot)
            {
                float reach = Mathf.Sin(progress * Mathf.PI);
                if (webActionLeft)
                {
                    pose.leftHand = Vector3.Lerp(pose.leftHand,
                        new Vector3(-0.44f, 1.51f, 0.83f), reach);
                    pose.leftElbow = Vector3.Lerp(pose.leftElbow,
                        new Vector3(-0.46f, 1.37f, 0.31f), reach);
                }
                else
                {
                    pose.rightHand = Vector3.Lerp(pose.rightHand,
                        new Vector3(0.44f, 1.51f, 0.83f), reach);
                    pose.rightElbow = Vector3.Lerp(pose.rightElbow,
                        new Vector3(0.46f, 1.37f, 0.31f), reach);
                }
                pose.lean = Mathf.Lerp(pose.lean, -10f, reach);
                return;
            }

            // Reach along the taut strand, then snap that hand back to the chest.
            float snap = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((progress - 0.18f) / 0.58f));
            Vector3 yankHand = Vector3.Lerp(
                new Vector3(webActionLeft ? -0.43f : 0.43f, 1.56f, 0.68f),
                new Vector3(webActionLeft ? -0.22f : 0.22f, 1.26f, -0.04f), snap);
            Vector3 yankElbow = Vector3.Lerp(
                new Vector3(webActionLeft ? -0.51f : 0.51f, 1.39f, 0.27f),
                new Vector3(webActionLeft ? -0.51f : 0.51f, 1.13f, -0.15f), snap);
            if (webActionLeft)
            {
                pose.leftHand = yankHand;
                pose.leftElbow = yankElbow;
                pose.rightHand = new Vector3(0.47f, 1.13f, 0.19f);
                pose.rightElbow = new Vector3(0.50f, 1.30f, 0.08f);
            }
            else
            {
                pose.rightHand = yankHand;
                pose.rightElbow = yankElbow;
                pose.leftHand = new Vector3(-0.47f, 1.13f, 0.19f);
                pose.leftElbow = new Vector3(-0.50f, 1.30f, 0.08f);
            }
            pose.leftKnee = new Vector3(-0.23f, 0.63f, 0.16f);
            pose.rightKnee = new Vector3(0.23f, 0.63f, 0.16f);
            pose.leftFoot = new Vector3(-0.23f, 0.23f, -0.16f);
            pose.rightFoot = new Vector3(0.23f, 0.23f, -0.16f);
            pose.lean = Mathf.Lerp(-4f, -20f, snap);
        }

        private Vector3 ReachTowardAnchor(Vector3 shoulder, Vector3 worldAnchor)
        {
            Vector3 direction = appearance.InverseTransformPoint(worldAnchor) - shoulder;
            return shoulder + direction.normalized * 0.79f;
        }

        private static void PositionBone(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            Vector3 axis = to - from;
            bone.localPosition = (from + to) * 0.5f;
            bone.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            bone.localScale = new Vector3(radius, axis.magnitude * 0.5f, radius);
        }

        private static void PositionJoint(Transform joint, Vector3 position, float diameter)
        {
            joint.localPosition = position;
            joint.localScale = Vector3.one * diameter;
        }

        private static void PositionBand(Transform band, Vector3 from, Vector3 to,
            float along, float width, float height)
        {
            Vector3 axis = to - from;
            band.localPosition = Vector3.Lerp(from, to, along);
            band.localRotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            band.localScale = new Vector3(width, height, width);
        }

        private static void PositionFoot(Transform foot, Vector3 position)
        {
            foot.localPosition = position;
            foot.localScale = new Vector3(0.23f, 0.13f, 0.35f);
        }
    }
}
