using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace Persona
{
    public class Contour : MonoBehaviour
    {
        public GameObject actor;
        public Transform actorRoot;

        public GameObject[] clothing;

        //Rate limiting until I can figure out how to optimize ApplyMorphs in DQS.
        public float updateRate = 1.0f;

        private SkinnedMeshRenderer skinnedMeshRenderer;

        private DualQuaternionSkinner dualQuaternionSkinner;

        private Mesh skinnedMesh;
        int blendShapeCount;

        private int blendShapeIndex;

        private IEnumerator sculptCoroutine;

        float[] morphWeights;

        string[] bonesToWatch = {
            "head", "neckUpper", "neckLower", "abdomenUpper", "abdomenLower", "chestUpper", "chestLower", "lCollar", "rCollar", "lShldrBend", "rShldrBend",
            "lForearmBend", "rForearmBend", "lHand", "rHand", "pelvis", "lThighBend", "rThighBend", "lShin", "rShin", "lFoot", "rFoot"
        };


        public class BlendShapeDriver
        {
            public string boneName;
            public string axis;
            public string morphName;
            public float target;
            public float clampLow;
            public float clampHigh;

            public BlendShapeDriver(string mn, string bn, string a, float t, float cl = 0, float ch = 100)
            {
                boneName = bn;
                axis = a;
                morphName = mn;
                target = t;
                clampLow = cl;
                clampHigh = ch;
            }
        }

        public List<BlendShapeDriver> blendShapeDrivers = new List<BlendShapeDriver>()
            {
                new BlendShapeDriver("pJCMPelvisFwd_25","pelvis","x",-25),

                new BlendShapeDriver("pJCMAbdomenFwd_35","abdomenLower","x",35),

                new BlendShapeDriver("pJCMAbdomen2Fwd_40","abdomenUpper","x",40),
                new BlendShapeDriver("pJCMAbdomen2Side_24_L","abdomenUpper","z",-24),
                new BlendShapeDriver("pJCMAbdomen2Side_24_R","abdomenUpper","z",24),

                new BlendShapeDriver("pJCMChestFwd_35","chestLower","x",35),
                new BlendShapeDriver("pJCMChestSide_20_L","chestLower","z",-20),
                new BlendShapeDriver("pJCMChestSide_20_R","chestLower","z",20),

                new BlendShapeDriver("pJCMNeckLowerSide_40_L","neckLower","z",-40),
                new BlendShapeDriver("pJCMNeckLowerSide_40_R","neckLower","z",40),
                new BlendShapeDriver("pJCMNeckTwist_22_L","neckLower","y",22),
                new BlendShapeDriver("pJCMNeckTwist_22_R","neckLower","y",-22),

                new BlendShapeDriver("pJCMNeckBack_27","neckUpper","x",-27),
                new BlendShapeDriver("pJCMNeckFwd_35","neckUpper","x",35),

                new BlendShapeDriver("pJCMHeadBack_27","head","x",-27),
                new BlendShapeDriver("pJCMHeadFwd_25","head","x",25),

                new BlendShapeDriver("pJCMCollarTwist_n30_L","lCollar","x",-30),
                new BlendShapeDriver("pJCMCollarTwist_n30_R","rCollar","x",-30),
                new BlendShapeDriver("pJCMCollarTwist_p30_L","lCollar","x",30),
                new BlendShapeDriver("pJCMCollarTwist_p30_R","rCollar","x",30),
                new BlendShapeDriver("pJCMCollarUp_55_L","lCollar","z",55),
                new BlendShapeDriver("pJCMCollarUp_55_R","rCollar","z",-55),

                new BlendShapeDriver("pJCMShldrDown_40_L","lShldrBend","z",40),
                new BlendShapeDriver("pJCMShldrDown_40_R","rShldrBend","z",-40),
                new BlendShapeDriver("pJCMShldrFwd_110_L","lShldrBend","y",110),
                new BlendShapeDriver("pJCMShldrFwd_110_R","rShldrBend","y",-110),
                new BlendShapeDriver("pJCMShldrUp_90_L","lShldrBend","z",-90),
                new BlendShapeDriver("pJCMShldrUp_90_R","rShldrBend","z",90),

                new BlendShapeDriver("pJCMForeArmFwd_135_L","lForearmBend","y",135),
                new BlendShapeDriver("pJCMForeArmFwd_135_R","rForearmBend","y",-135),
                new BlendShapeDriver("pJCMForeArmFwd_75_L","lForearmBend","y",75),
                new BlendShapeDriver("pJCMForeArmFwd_75_R","rForearmBend","y",-75),

                new BlendShapeDriver("pJCMHandDwn_70_L","lHand","z",-70),
                new BlendShapeDriver("pJCMHandDwn_70_R","rHand","z",70),
                new BlendShapeDriver("pJCMHandUp_80_L","lHand","z",80),
                new BlendShapeDriver("pJCMHandUp_80_R","rHand","z",-80),


                new BlendShapeDriver("pJCMThighBack_35_L","lThighBend","x",35),
                new BlendShapeDriver("pJCMThighBack_35_R","rThighBend","x",35),
                new BlendShapeDriver("pJCMThighFwd_115_L","lThighBend","x",-115),
                new BlendShapeDriver("pJCMThighFwd_115_R","rThighBend","x",-115),
                new BlendShapeDriver("pJCMThighFwd_57_L","lThighBend","x",-57),
                new BlendShapeDriver("pJCMThighFwd_57_R","rThighBend","x",-57),
                new BlendShapeDriver("pJCMThighSide_85_L","lThighBend","z",-85),
                new BlendShapeDriver("pJCMThighSide_85_R","rThighBend","z",85),

                new BlendShapeDriver("pJCMShinBend_155_L","lShin","x",155),
                new BlendShapeDriver("pJCMShinBend_155_R","rShin","x",155),
                new BlendShapeDriver("pJCMShinBend_90_L","lShin","x",90),
                new BlendShapeDriver("pJCMShinBend_90_R","rShin","x",90),

                new BlendShapeDriver("pJCMFootDwn_75_L","lFoot","x",75),
                new BlendShapeDriver("pJCMFootDwn_75_R","rFoot","x",75),
                new BlendShapeDriver("pJCMFootUp_40_L","lFoot","x",-40),
                new BlendShapeDriver("pJCMFootUp_40_R","rFoot","x",-40),

                //new BlendShapeDriver("pJCMToesUp_60_L","lToe","x",-60,0,100),
                //new BlendShapeDriver("pJCMToesUp_60_R","rToe","x",-60,0,100),

                //new BlendShapeDriver("pJCMBigToeDown_45_L","lBigToe","x",45,0,100),
                //new BlendShapeDriver("pJCMBigToeDown_45_R","rBigToe","x",45,0,100),
            };



        void Awake()
        {
            skinnedMeshRenderer = actor.GetComponent<SkinnedMeshRenderer>();
            dualQuaternionSkinner = actor.GetComponent<DualQuaternionSkinner>();

            skinnedMesh = skinnedMeshRenderer.sharedMesh;
        }

        void Start()
        {
            blendShapeCount = skinnedMesh.blendShapeCount;
            morphWeights = new float[blendShapeCount];

            sculptCoroutine = Sculpt();
            StartCoroutine(sculptCoroutine);

        }

        IEnumerator Sculpt()
        {
            while (true)
            {
                CollectWeights();
                SetWeights();
                yield return new WaitForSeconds(updateRate);
            }
        }

        public void SetBlendShapeWeight(int blendShapeIndex, float value)
        {
            if (value > 5 || value < -5)
            {
                dualQuaternionSkinner.SetBlendShapeWeight(blendShapeIndex, value);
            }
        }

        public void SetWeights()
        {
            dualQuaternionSkinner.SetBlendShapeWeights(morphWeights);

            //Assumes clothing has same collection of blendshapes, in same order. Should be the case when sing the Daz bridge.
            foreach (var clothingItem in clothing)
            {
                var clothingSkinner = clothingItem.GetComponent<DualQuaternionSkinner>();
                if (clothingSkinner != null)
                {
                    clothingSkinner.SetBlendShapeWeights(morphWeights);
                }
            }
        }

        public float GetDriverValueForRotation(BlendShapeDriver driver, float rotation)
        {
            rotation = (rotation > 180) ? rotation - 360 : rotation;

            float percent = (rotation / driver.target) * 100;

            percent = Mathf.Clamp(percent, driver.clampLow, driver.clampHigh);

            //Noise reduction.
            if (percent < 10)
            {
                percent = 0;
            }

            return percent;

        }

        public void CollectWeights()
        {
            foreach (string boneName in bonesToWatch)
            {

                var tranformForBone = GetTransformForBone(boneName);
                var driversForBone = blendShapeDrivers.Where(d => d.boneName == boneName).ToList();

                foreach (var driver in driversForBone)
                {

                    blendShapeIndex = GetBlendShapeId(skinnedMeshRenderer, driver.morphName);

                    if (blendShapeIndex != -1)
                    {

                        float rotation = 0;

                        if (driver.axis == "x")
                        {
                            rotation = tranformForBone.localEulerAngles.x;

                        }
                        else if (driver.axis == "y")
                        {
                            rotation = tranformForBone.localEulerAngles.y;

                        }
                        else if (driver.axis == "z")
                        {
                            rotation = tranformForBone.localEulerAngles.z;
                        }

                        morphWeights[blendShapeIndex] = GetDriverValueForRotation(driver, rotation);
                    }

                }
            }

        }

        void OnDisable()
        {
            StopCoroutine(sculptCoroutine);
        }

        void OnDestroy()
        {
            StopCoroutine(sculptCoroutine);
        }

        public int GetBlendShapeId(SkinnedMeshRenderer mesh, string shapeName)
        {
            Mesh m = mesh.sharedMesh;
            int id = 0;

            for (int i = 0; i < m.blendShapeCount; i++)
            {
                string name = m.GetBlendShapeName(i);
                if (name.ToUpper().Trim().Contains(shapeName.ToUpper().Trim()))
                {
                    id = i;
                }
            }
            return id;
        }

        public Transform GetTransformForBone(string bone)
        {
            var allTransforms = actorRoot.GetComponentsInChildren<Transform>();
            return allTransforms.First(k => k.gameObject.name == bone);
        }
    }
}

