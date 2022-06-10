using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;

namespace ExMachina
{
    public class Contour : MonoBehaviour
    {
        public Core core;
        public GameObject modelMesh;
        public Transform actorRoot;
       
        public List<GameObject> clothing;

        public bool useDQSMesh = true;

        public int framesPerUpdate = 10;
        int framesThisUpdate = 0;

        private SkinnedMeshRenderer skinnedMeshRenderer;

        private DualQuaternionSkinner dualQuaternionSkinner;

        public SpeechBlend speechBlend;

        private Mesh skinnedMesh;
        int blendShapeCount;

        float[] morphWeights;

        private bool started;

        string[] bonesToWatch = {
            "head", "neckUpper", "neckLower", "abdomenUpper", "abdomenLower", "chestUpper", "chestLower", "lCollar", "rCollar", "lShldrBend", "rShldrBend",
            "lForearmBend", "rForearmBend", "pelvis", "lThighBend", "rThighBend", "lShin", "rShin"
        };

        Dictionary<int, float> morphWeightsToSend = new Dictionary<int, float>();

        Dictionary<string, List<BlendShapeDriver>> driversForBones = new Dictionary<string, List<BlendShapeDriver>>();

        List<BlendShape> initialBlendShapes = new List<BlendShape>();

        public class BlendShape
        {
            public string morphName;
            public int morphIndex;
            public float weight;

            public BlendShape(string mn, float w)
            {
                morphName = mn;
                weight = w;
            }
        }

        public class BlendShapeDriver
        {
            public string boneName;
            public string axis;
            public string morphName;
            public int morphIndex;
            public float targetDegrees;
            public float startDegrees;
            public Transform boneTransform;

            public BlendShapeDriver(string morphName, string boneName, string axis, float targetDegrees, float startDegrees = 0)
            {
                this.boneName = boneName;
                this.axis = axis;
                this.morphName = morphName;
                this.targetDegrees = targetDegrees;
                this.startDegrees = startDegrees;
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

                new BlendShapeDriver("pJCMForeArmFwd_135_L","lForearmBend","y",135,75),
                new BlendShapeDriver("pJCMForeArmFwd_135_R","rForearmBend","y",-135,-75),
                new BlendShapeDriver("pJCMForeArmFwd_75_L","lForearmBend","y",75),
                new BlendShapeDriver("pJCMForeArmFwd_75_R","rForearmBend","y",-75),

                //new BlendShapeDriver("pJCMHandDwn_70_L","lHand","z",-70),
                //new BlendShapeDriver("pJCMHandDwn_70_R","rHand","z",70),
                //new BlendShapeDriver("pJCMHandUp_80_L","lHand","z",80),
                //new BlendShapeDriver("pJCMHandUp_80_R","rHand","z",-80),


                new BlendShapeDriver("pJCMThighBack_35_L","lThighBend","x",35),
                new BlendShapeDriver("pJCMThighBack_35_R","rThighBend","x",35),
                new BlendShapeDriver("pJCMThighFwd_115_L","lThighBend","x",-115,-57),
                new BlendShapeDriver("pJCMThighFwd_115_R","rThighBend","x",-115,-57),
                new BlendShapeDriver("pJCMThighFwd_57_L","lThighBend","x",-57),
                new BlendShapeDriver("pJCMThighFwd_57_R","rThighBend","x",-57),
                new BlendShapeDriver("pJCMThighSide_85_L","lThighBend","z",-85),
                new BlendShapeDriver("pJCMThighSide_85_R","rThighBend","z",85),

                new BlendShapeDriver("pJCMShinBend_155_L","lShin","x",155, 90),
                new BlendShapeDriver("pJCMShinBend_155_R","rShin","x",155, 90),
                new BlendShapeDriver("pJCMShinBend_90_L","lShin","x",90),
                new BlendShapeDriver("pJCMShinBend_90_R","rShin","x",90),
                new BlendShapeDriver("pJCMFlexHamstring_L","lShin","x",155),
                new BlendShapeDriver("pJCMFlexHamstring_R","rShin","x",155),

                //new BlendShapeDriver("pJCMFootDwn_75_L","lFoot","x",75),
                //new BlendShapeDriver("pJCMFootDwn_75_R","rFoot","x",75),
                //new BlendShapeDriver("pJCMFootUp_40_L","lFoot","x",-40),
                //new BlendShapeDriver("pJCMFootUp_40_R","rFoot","x",-40),



                //new BlendShapeDriver("pJCMToesUp_60_L","lToe","x",-60,0,100),
                //new BlendShapeDriver("pJCMToesUp_60_R","rToe","x",-60,0,100),

                //new BlendShapeDriver("pJCMBigToeDown_45_L","lBigToe","x",45,0,100),
                //new BlendShapeDriver("pJCMBigToeDown_45_R","rBigToe","x",45,0,100),
            };



        void Awake()
        {
            started = true;
            core = GetComponent<Core>();
            speechBlend = GetComponent<SpeechBlend>();

            modelMesh = core.actorMesh;
            actorRoot = core.actorRootBone;

            skinnedMeshRenderer = modelMesh.GetComponent<SkinnedMeshRenderer>();
            dualQuaternionSkinner = modelMesh.GetComponent<DualQuaternionSkinner>();

            skinnedMesh = skinnedMeshRenderer.sharedMesh;

            blendShapeCount = skinnedMesh.blendShapeCount;
            morphWeights = new float[blendShapeCount];

            morphWeightsToSend = new Dictionary<int, float>();

            for (int i = 0; i < this.morphWeights.Length; i++)
            {
                this.morphWeights[i] = skinnedMeshRenderer.GetBlendShapeWeight(i);
            }


            foreach (string boneName in bonesToWatch)
            {
                var drivers = blendShapeDrivers.Where(d => d.boneName == boneName).ToList();
                foreach(var driver in drivers)
                {
                    driver.boneTransform = GetTransformForBone(boneName);
                    driver.morphIndex = GetBlendShapeId(skinnedMeshRenderer, driver.morphName);
                }
                driversForBones.Add(boneName, drivers);
            }

            initialBlendShapes.Add(new BlendShape("LipsPartCenter", 5f));
            initialBlendShapes.Add(new BlendShape("Fit_Toes", 55f));

            foreach (var blendShape in initialBlendShapes)
            {
                blendShape.morphIndex = GetBlendShapeId(skinnedMeshRenderer, blendShape.morphName);
            }

            
        }



        void Start()
        {
            SetInitialBlendShapes();
        }

        private void FixedUpdate()
        {
            framesThisUpdate++;

            if (framesThisUpdate >= framesPerUpdate)
            {
                if (speechBlend != null & !speechBlend.voiceAudioSource.isPlaying)
                {
                    CollectWeights();
                    SendWeights();
                }

                framesThisUpdate = 0;
            }
        }

        public void SetInitialBlendShapes()
        {
            foreach(var blendShape in initialBlendShapes)
            {
                morphWeights[blendShape.morphIndex] = blendShape.weight;
                morphWeightsToSend.Add(blendShape.morphIndex, blendShape.weight);
            }

            if (useDQSMesh)
            {
                dualQuaternionSkinner.SetBlendShapeDictionaryWeights(morphWeightsToSend);
            }
            else
            {
                for (int i = 0; i < morphWeights.Length; i++)
                {
                    skinnedMeshRenderer.SetBlendShapeWeight(i, morphWeights[i]);
                }

            }
        }

        public void SendAllWeightsForObject(GameObject gameObject)
        {
            if (!started)
                return;

            Dictionary<int, float> everyMorphWeight = new Dictionary<int, float>();

            for (int i = 0; i < morphWeights.Length; i++)
            {
                everyMorphWeight.Add(i, morphWeights[i]);
            }

            gameObject.GetComponent<DualQuaternionSkinner>().SetBlendShapeDictionaryWeights(everyMorphWeight);
        }


        public void SendWeights()
        {

            if (morphWeightsToSend.Count == 0)
                return;

            if (useDQSMesh)
            {
                dualQuaternionSkinner.SetBlendShapeDictionaryWeights(morphWeightsToSend);
            } else
            {
                for (int i = 0; i < morphWeights.Length; i++)
                {
                    skinnedMeshRenderer.SetBlendShapeWeight(i, morphWeights[i]);
                }
                
            }

            DualQuaternionSkinner clothingDQSSkinner;

            //Assumes clothing has same collection of blendshapes, in same order. Should be the case when sing the Daz bridge.
            foreach (var clothingItem in clothing)
            {
                if (clothingItem.activeInHierarchy)
                {

                    clothingDQSSkinner = clothingItem.GetComponent<DualQuaternionSkinner>();

                    if (clothingDQSSkinner != null && useDQSMesh)
                    {
                        clothingDQSSkinner.SetBlendShapeDictionaryWeights(morphWeightsToSend);
                    } else
                    {
                        for (int i = 0; i < morphWeights.Length; i++)
                        {
                            clothingItem.GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(i, morphWeights[i]);
                        }
                    }
                }
            }

            morphWeightsToSend = new Dictionary<int, float>();
        }

        public float GetDriverValueForRotation(Vector3 vectorCurrent, Vector3 vectorTarget, Vector3 vectorReference, Vector3 vectorStart)
        {

            float blendShapeWeight;

            //smaller angles mean closer to target
            var angleStartToTarget = Quaternion.Angle(Quaternion.Euler(vectorStart), Quaternion.Euler(vectorTarget));
            var angleCurrentToTarget = Quaternion.Angle(Quaternion.Euler(vectorCurrent), Quaternion.Euler(vectorTarget));       

            var angleCurrentToReference = Quaternion.Angle(Quaternion.Euler(vectorCurrent), Quaternion.Euler(vectorReference));
            var angleTargetToReference = Quaternion.Angle(Quaternion.Euler(vectorTarget), Quaternion.Euler(vectorReference));

            //we haven't reached the start point for morph
            if (angleCurrentToTarget > angleStartToTarget)
            {
                blendShapeWeight = 0f;
            }
            //we've passed the top limit
            else if (angleCurrentToReference < angleTargetToReference)
            {
                blendShapeWeight = 1f;
            } else
            {
                blendShapeWeight = 1f - Mathf.Clamp01(angleCurrentToTarget / angleStartToTarget);
            }

            blendShapeWeight = blendShapeWeight * 100f;

            if (blendShapeWeight < 5f)
            {
                blendShapeWeight = 0f;
            }
            return blendShapeWeight;

        }

        public void CollectWeights()
        {
            //Debug.Log("weights to send: " + morphWeightsToSend.Count);

            if (!started)
                return;

            morphWeightsToSend = new Dictionary<int, float>();

            foreach (string boneName in bonesToWatch)
            {

                foreach (var driver in driversForBones[boneName])
                {

                    var vectorTarget = new Vector3(0f,0f,0f);
                    var vectorReference = new Vector3(0f,0f,0f);
                    var vectorStart = new Vector3(0f,0f,0f);

                    if (driver.morphIndex != -1)
                    {
                        Vector3 currentVector3Rotation = driver.boneTransform.localEulerAngles;

                        float referenceDegrees = driver.targetDegrees > 0 ? 180f : -180f;

                        if (driver.axis == "x")
                        {
                            vectorTarget = new Vector3(driver.targetDegrees, 0f, 0f);
                            vectorReference = new Vector3(referenceDegrees, 0f, 0f);
                            vectorStart = new Vector3(driver.startDegrees, 0f, 0f);
                        }
                        else if (driver.axis == "y")
                        {
                            vectorTarget = new Vector3(0f, driver.targetDegrees, 0f);
                            vectorReference = new Vector3(0f, referenceDegrees, 0f);
                            vectorStart = new Vector3(0f, driver.startDegrees, 0f);
                        }
                        else if (driver.axis == "z")
                        {
                            vectorTarget = new Vector3(0f, 0f, driver.targetDegrees);
                            vectorReference = new Vector3(0f, 0f, referenceDegrees);
                            vectorStart = new Vector3(0f, 0f, driver.startDegrees);
                        }

                        var weight = GetDriverValueForRotation(currentVector3Rotation, vectorTarget, vectorReference, vectorStart);

                        if (weight > 2.0f)
                        {
                            if (weight < (this.morphWeights[driver.morphIndex] - 1.0f) || weight > (this.morphWeights[driver.morphIndex] + 1.0f)) { 
                                morphWeights[driver.morphIndex] = weight;
                                morphWeightsToSend.Add(driver.morphIndex, weight);
                            }
                        }
                    }

                }
            }

        }

        void OnDisable()
        {
            //StopCoroutine(sculptCoroutine);
        }

        void OnDestroy()
        {
            //StopCoroutine(sculptCoroutine);
        }

        public int GetBlendShapeId(SkinnedMeshRenderer mesh, string shapeName)
        {
            Mesh m = mesh.sharedMesh;
            int id = 0;

            for (int i = 0; i < m.blendShapeCount; i++)
            {
                string name = m.GetBlendShapeName(i);
                if (name.ToUpper().Trim().EndsWith(shapeName.ToUpper().Trim()))
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

        //if (driver.morphName == "pJCMThighFwd_115_L")
        //{
        //    Debug.Log("name: pJCMThighFwd_115_L");
        //    Debug.Log("vectorTarget: " + vectorTarget);
        //    Debug.Log("vectorReference: " + vectorReference);
        //    Debug.Log("vectorStart: " + vectorStart);
        //    Debug.Log("angleStartToTarget: " + angleStartToTarget);
        //    Debug.Log("angleCurrentToTarget: " + angleCurrentToTarget);
        //    Debug.Log("angleCurrentToReference: " + angleCurrentToReference);
        //    Debug.Log("angleZeroToTarget: " + angleZeroToTarget);
        //    Debug.Log("blendShapeWeight: " + blendShapeWeight);

        //}
    }
}

