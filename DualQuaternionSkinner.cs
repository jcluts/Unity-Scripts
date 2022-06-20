using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class DualQuaternionSkinner : MonoBehaviour
{
	/// <summary>
	/// Bone orientation is required for bulge-compensation.<br>
	/// Do not set directly, use custom editor instead.
	/// </summary>
	public Vector3 boneOrientationVector = Vector3.up;

	bool viewFrustrumCulling = false;

	struct VertexInfo
	{

		public Vector4 position;
		public Vector4 normal;
		public Vector4 tangent;

		public int boneIndex0;
		public int boneIndex1;
		public int boneIndex2;
		public int boneIndex3;

		public float weight0;
		public float weight1;
		public float weight2;
		public float weight3;

	}

	struct MorphDelta
	{

		public Vector4 position;
		public Vector4 normal;
		public Vector4 tangent;
	}

	struct DualQuaternion
	{
		public Quaternion rotationQuaternion;
		public Vector4 position;
	}

	const int numthreads = 8;    // must be same in compute shader code

	public ComputeShader shaderComputeBoneDQ;
	public ComputeShader shaderDQBlend;
	public ComputeShader shaderApplyMorph;

	public bool started { get; private set; } = false;

	Matrix4x4[] poseMatrices;

	ComputeBuffer bufPoseMatrices;
	ComputeBuffer bufSkinnedDq;
	ComputeBuffer bufBindDq;

	ComputeBuffer bufVertInfo;
	ComputeBuffer bufMorphTemp_1;
	ComputeBuffer bufMorphTemp_2;

	ComputeBuffer bufBoneDirections;

	ComputeBuffer[] arrBufMorphDeltas;

	float[] morphWeights;
	float[] lastMorphWeights;

	List<int> updatedMorphs = new List<int>();

	GraphicsBuffer vertexBuffer;

	public MeshFilter mf
	{
		get
		{
			if (_mf == null)
			{
				_mf = GetComponent<MeshFilter>();
			}

			return _mf;
		}
	}
	MeshFilter _mf;

	public MeshRenderer mr
	{
		get
		{
			if (_mr == null)
			{
				_mr = GetComponent<MeshRenderer>();
				if (_mr == null)
				{
					_mr = gameObject.AddComponent<MeshRenderer>();
				}
			}

			return _mr;
		}
	}
	MeshRenderer _mr;

	public SkinnedMeshRenderer smr
	{
		get
		{
			if (_smr == null)
			{
				_smr = GetComponent<SkinnedMeshRenderer>();
			}

			return _smr;
		}
	}
	SkinnedMeshRenderer _smr;

	public Mesh mesh
	{
		get
		{

			if (started == false)
			{
				//return smr.sharedMesh;
				return mf.mesh;
			}
			return mf.mesh;
		}
	}


	Transform[] bones;
	Matrix4x4[] bindPoses;

	int kernelHandleComputeBoneDQ;
	int kernelHandleDQBlend;
	int kernelHandleApplyMorph;

	public void SetViewFrustrumCulling(bool viewFrustrumculling)
	{
		if (viewFrustrumCulling == viewFrustrumculling)
			return;

		viewFrustrumCulling = viewFrustrumculling;

		if (started == true)
			UpdateViewFrustrumCulling();
	}

	public bool GetViewFrustrumCulling()
	{
		return viewFrustrumCulling;
	}

	void UpdateViewFrustrumCulling()
	{
		if (viewFrustrumCulling)
			mf.mesh.bounds = smr.localBounds;
		else
			mf.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 5.0f);
	}

	public float[] GetBlendShapeWeights()
	{
		float[] weights = new float[morphWeights.Length];
		for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = morphWeights[i];
        }

        return weights;
	}

	public float GetBlendShapeWeight(int index)
	{
		if (started == false)
		{
			return GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(index);
		}

		if (index < 0 || index >= morphWeights.Length)
		{
			throw new System.IndexOutOfRangeException("Blend shape index out of range");
		}

		return morphWeights[index];
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

	public void SetBlendShapeWeight(int index, float weight)
	{
		updatedMorphs = new List<int>();

		if (started == false)
		{
			GetComponent<SkinnedMeshRenderer>().SetBlendShapeWeight(index, weight);
			return;
		}

		if (index < 0 || index >= morphWeights.Length)
		{
			throw new System.IndexOutOfRangeException("Blend shape index out of range");
		}

		if (weight < (morphWeights[index] - 1.0f) || weight > (morphWeights[index] + 1.0f)) { 
			morphWeights[index] = weight;

			updatedMorphs.Add(index);
		}

		if (updatedMorphs.Count > 0)
		{
			ApplyMorphs();
		}
	}

	public void SetBlendShapeDictionaryWeights(Dictionary<int,float> newWeights)
	{

		updatedMorphs = new List<int>();

		if (morphWeights == null)
			return;
		
		foreach (KeyValuePair<int, float> newWeight in newWeights)
		{
			var index = newWeight.Key;
			var weight = newWeight.Value;



			if (index < 0 || index >= morphWeights.Length)
			{
				throw new System.IndexOutOfRangeException("Blend shape index out of range");
			}

			if (weight < (morphWeights[index] - 1.0f) || weight > (morphWeights[index] + 1.0f))
			{

				morphWeights[index] = weight;
				updatedMorphs.Add(index);
			}

			if (updatedMorphs.Count > 0)
            {
				ApplyMorphs();
			}
		}

	}

	void ApplyMorphs()
	{

        ZeroVertexInfos(
            bufVertInfo,
            ref bufMorphTemp_1,
            ref bufMorphTemp_2,
            arrBufMorphDeltas,
            morphWeights
        );

        ComputeBuffer bufMorphedVertexInfos = GetMorphedVertexInfos(
			bufVertInfo,
			ref bufMorphTemp_1,
			ref bufMorphTemp_2,
			arrBufMorphDeltas,
			morphWeights
		);

		shaderDQBlend.SetBuffer(kernelHandleDQBlend, "vertex_infos", bufMorphedVertexInfos);

		for (int i = 0; i < morphWeights.Length; i++)
		{
			lastMorphWeights[i] = morphWeights[i];
		}
	}

	ComputeBuffer bufLast;

	ComputeBuffer ZeroVertexInfos(ComputeBuffer bufOriginal, ref ComputeBuffer bufTemp_Target, ref ComputeBuffer bufTemp_2, ComputeBuffer[] arrBufDelta, float[] weights)
	{
		ComputeBuffer bufSource = bufLast;

		foreach (int updatedMorph in updatedMorphs)
		{

			shaderApplyMorph.SetBuffer(kernelHandleApplyMorph, "source", bufSource);
			shaderApplyMorph.SetBuffer(kernelHandleApplyMorph, "target", bufTemp_Target);
			shaderApplyMorph.SetBuffer(kernelHandleApplyMorph, "delta", arrBufDelta[updatedMorph]);
			shaderApplyMorph.SetFloat("weight", (lastMorphWeights[updatedMorph] / 100f) * -1);

			int numThreadGroups = bufSource.count / numthreads;
			if (bufSource.count % numthreads != 0)
			{
				numThreadGroups++;
			}

			shaderApplyMorph.Dispatch(kernelHandleApplyMorph, numThreadGroups, 1, 1);

			bufSource = bufTemp_Target;
			bufTemp_Target = bufTemp_2;
			bufTemp_2 = bufSource;
		}
		bufLast = bufSource;
		return bufSource;
	}



	ComputeBuffer GetMorphedVertexInfos(ComputeBuffer bufOriginal, ref ComputeBuffer butTemp_Target, ref ComputeBuffer bufTemp_2, ComputeBuffer[] arrBufDelta, float[] weights)
	{
		ComputeBuffer bufSource = bufLast;

		foreach (int updatedMorph in updatedMorphs)
		{
			
			if (weights[updatedMorph] == 0)
			{
				continue;
			}

			if (arrBufDelta[updatedMorph] == null)
			{
				throw new System.NullReferenceException();
			}

			var newMorphWeight = weights[updatedMorph] / 100f;

			shaderApplyMorph.SetBuffer(kernelHandleApplyMorph, "source", bufSource);
			shaderApplyMorph.SetBuffer(kernelHandleApplyMorph, "target", butTemp_Target);
			shaderApplyMorph.SetBuffer(kernelHandleApplyMorph, "delta", arrBufDelta[updatedMorph]);
			shaderApplyMorph.SetFloat("weight", newMorphWeight);

			int numThreadGroups = bufSource.count / numthreads;
			if (bufSource.count % numthreads != 0)
			{
				numThreadGroups++;
			}

			shaderApplyMorph.Dispatch(kernelHandleApplyMorph, numThreadGroups, 1, 1);

			bufSource = butTemp_Target;
			butTemp_Target = bufTemp_2;
			bufTemp_2 = bufSource;
		}

		bufLast = bufSource;
		return bufSource;
	}


	void GrabMeshFromSkinnedMeshRenderer()
	{
		ReleaseBuffers();

		//mf.mesh = smr.sharedMesh;
		bindPoses = mf.mesh.bindposes;

		var bindPosesLength = mf.mesh.bindposes.Length;
		var vertexCount = mf.mesh.vertexCount;
		var blendShapeCount = mf.mesh.blendShapeCount;

		arrBufMorphDeltas = new ComputeBuffer[blendShapeCount];

		morphWeights = new float[blendShapeCount];
		lastMorphWeights = new float[blendShapeCount];

		var deltaVertices = new Vector3[vertexCount];
		var deltaNormals = new Vector3[vertexCount];
		var deltaTangents = new Vector3[vertexCount];
		var deltaVertInfos = new MorphDelta[vertexCount];

		for (int i = 0; i < blendShapeCount; i++)
		{
			mf.mesh.GetBlendShapeFrameVertices(i, 0, deltaVertices, deltaNormals, deltaTangents);

			arrBufMorphDeltas[i] = new ComputeBuffer(vertexCount, sizeof(float) * 12);

			for (int k = 0; k < vertexCount; k++)
			{
				deltaVertInfos[k].position	= deltaVertices	!= null ? deltaVertices[k]	: Vector3.zero;
				deltaVertInfos[k].normal	= deltaNormals	!= null ? deltaNormals[k]	: Vector3.zero;
				deltaVertInfos[k].tangent	= deltaTangents	!= null ? deltaTangents[k]	: Vector3.zero;
			}

			arrBufMorphDeltas[i].SetData(deltaVertInfos);
		}

		mr.materials = smr.sharedMaterials;

		poseMatrices = new Matrix4x4[bindPosesLength];

		bufPoseMatrices = new ComputeBuffer(bindPosesLength, sizeof(float) * 16);
		shaderComputeBoneDQ.SetBuffer(kernelHandleComputeBoneDQ, "pose_matrices", bufPoseMatrices);

		bufSkinnedDq = new ComputeBuffer(bindPosesLength, sizeof(float) * 8);
		shaderComputeBoneDQ.SetBuffer(kernelHandleComputeBoneDQ, "skinned_dual_quaternions", bufSkinnedDq);
		shaderDQBlend.SetBuffer(kernelHandleDQBlend, "skinned_dual_quaternions", bufSkinnedDq);

		bufBoneDirections = new ComputeBuffer(bindPosesLength, sizeof(float) * 4);
		shaderComputeBoneDQ.SetBuffer(kernelHandleComputeBoneDQ, "bone_directions", bufBoneDirections);
		shaderDQBlend.SetBuffer(kernelHandleDQBlend, "bone_directions", bufBoneDirections);

		bufVertInfo = new ComputeBuffer(vertexCount, sizeof(float) * 16 + sizeof(int) * 4);
		bufLast = new ComputeBuffer(vertexCount, sizeof(float) * 16 + sizeof(int) * 4);

		shaderDQBlend.SetInt("vertex_count", vertexCount);

		var vertInfos = new VertexInfo[vertexCount];
		Vector3[] vertices = mf.mesh.vertices;
		Vector3[] normals = mf.mesh.normals;
		Vector4[] tangents = mf.mesh.tangents;
		BoneWeight[] boneWeights = mf.mesh.boneWeights;

		for (int i = 0; i < vertInfos.Length; i++)
		{
			vertInfos[i].position = vertices[i];

			vertInfos[i].boneIndex0 = boneWeights[i].boneIndex0;
			vertInfos[i].boneIndex1 = boneWeights[i].boneIndex1;
			vertInfos[i].boneIndex2 = boneWeights[i].boneIndex2;
			vertInfos[i].boneIndex3 = boneWeights[i].boneIndex3;

			vertInfos[i].weight0 = boneWeights[i].weight0;
			vertInfos[i].weight1 = boneWeights[i].weight1;
			vertInfos[i].weight2 = boneWeights[i].weight2;
			vertInfos[i].weight3 = boneWeights[i].weight3;

		}

		if (normals.Length > 0)
		{
			for (int i = 0; i < vertInfos.Length; i++)
			{
				vertInfos[i].normal = normals[i];
			}
		}

		if (tangents.Length > 0)
		{
			for (int i = 0; i < vertInfos.Length; i++)
			{
				vertInfos[i].tangent = tangents[i];
			}
		}

		bufVertInfo.SetData(vertInfos);
		bufLast.SetData(vertInfos);
		shaderDQBlend.SetBuffer(kernelHandleDQBlend, "vertex_infos", bufVertInfo);
		
		bufMorphTemp_1 = new ComputeBuffer(vertexCount, sizeof(float) * 16 + sizeof(int) * 4);
		bufMorphTemp_2 = new ComputeBuffer(vertexCount, sizeof(float) * 16 + sizeof(int) * 4);

		// bind DQ buffer

		var bindDqs = new DualQuaternion[bindPoses.Length];
		for (int i = 0; i < bindPoses.Length; i++)
		{
			bindDqs[i].rotationQuaternion	= bindPoses[i].ExtractRotation();
			bindDqs[i].position				= bindPoses[i].ExtractPosition();
		}

		bufBindDq = new ComputeBuffer(bindDqs.Length, sizeof(float) * 8);
		bufBindDq.SetData(bindDqs);
		shaderComputeBoneDQ.SetBuffer(kernelHandleComputeBoneDQ, "bind_dual_quaternions", bufBindDq);

		mf.mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
		vertexBuffer = mf.mesh.GetVertexBuffer(0);

		shaderDQBlend.SetBuffer(kernelHandleDQBlend, "Vertices", vertexBuffer);

		UpdateViewFrustrumCulling();
		ApplyMorphs();
	}

	void ReleaseBuffers()
	{
		bufBindDq?.Release();
		bufPoseMatrices?.Release();
		bufSkinnedDq?.Release();

		bufVertInfo?.Release();
		bufMorphTemp_1?.Release();
		bufMorphTemp_2?.Release();

		bufLast?.Release();

		bufBoneDirections?.Release();

		vertexBuffer?.Release();

		if (arrBufMorphDeltas != null)
		{
			for (int i = 0; i < arrBufMorphDeltas.Length; i++)
			{
				arrBufMorphDeltas[i]?.Release();
			}
		}
	}
	void OnDestroy()
	{
		ReleaseBuffers();
	}

	void Init()
	{
		mf.mesh.MarkDynamic();
	}

	ComputeShader GetShader(string shaderName)
	{
		ComputeShader cs = null;
		ComputeShader[] compShaders = (ComputeShader[])Resources.FindObjectsOfTypeAll(typeof(ComputeShader));
		for (int i = 0; i < compShaders.Length; i++)
		{
			if (compShaders[i].name == shaderName)
			{
				cs = compShaders[i];
				break;
			}
		}
		return cs;
	}

    private void Awake()
    {

		shaderComputeBoneDQ = (ComputeShader)Instantiate(GetShader("ComputeBoneDQ"));
		shaderDQBlend = (ComputeShader)Instantiate(GetShader("DQBlend"));
		shaderApplyMorph = (ComputeShader)Instantiate(GetShader("ApplyMorph"));
		
		kernelHandleComputeBoneDQ = shaderComputeBoneDQ.FindKernel("CSMain");
		kernelHandleDQBlend = shaderDQBlend.FindKernel("CSMain");
		kernelHandleApplyMorph = shaderApplyMorph.FindKernel("CSMain");

		bones = smr.bones;

		started = true;
		GrabMeshFromSkinnedMeshRenderer();

		for (int i = 0; i < morphWeights.Length; i++)
		{
			morphWeights[i] = smr.GetBlendShapeWeight(i);
			lastMorphWeights[i] = smr.GetBlendShapeWeight(i);
		}	

	}


	void Start()
	{
		smr.enabled = false;
	}

	void Update()
	{
		if (mr.isVisible == false)
		{
			return;
		}

		for (int i = 0; i < bones.Length; i++)
		{
			poseMatrices[i] = bones[i].localToWorldMatrix;
		}
		
		bufPoseMatrices.SetData(poseMatrices);

		int numThreadGroups = bones.Length / numthreads;
		numThreadGroups += bones.Length % numthreads == 0 ? 0 : 1;

        shaderComputeBoneDQ.SetVector("boneOrientation", boneOrientationVector);
        shaderComputeBoneDQ.SetMatrix(
            "self_matrix",
            transform.worldToLocalMatrix
        );
        shaderComputeBoneDQ.Dispatch(kernelHandleComputeBoneDQ, numThreadGroups, 1, 1);

		numThreadGroups = mf.mesh.vertexCount / numthreads;
		numThreadGroups += mf.mesh.vertexCount % numthreads == 0 ? 0 : 1;

		shaderDQBlend.Dispatch(kernelHandleDQBlend, numThreadGroups, 1, 1);

	}
}