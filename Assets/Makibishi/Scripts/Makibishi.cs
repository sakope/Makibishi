using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MakibishiParticleSystem
{
    public interface IMakibishi
    {
        void Play();
        void Stop();
    }

    struct RewritableBuffer
	{
		public Vector4 position;
		public Vector4 velocity;
		public Vector4 rotation;
	}

    //[ExecuteInEditMode]
    [AddComponentMenu("Effects/GPU Particle System/Makibishi")]
    public class Makibishi : MonoBehaviour, IMakibishi
    {
        #region GPU thread and render texture buffers

        private const int THREAD_GROUP = 64;
        private const int MAX_DISPATCH = 65535;
        private const int MAX_VERTICES = 65535;
        private const int MAX_EMIT     = MAX_DISPATCH * THREAD_GROUP + 1 - THREAD_GROUP;
        private const int MAX_TEXTURE  = 4096;

        #endregion

        #region Global setting

        public enum GPGPUMode
        {
            ComputeShader,
            MultipleRenderTarget,
        }

        [SerializeField]
        private GPGPUMode _usingGPGPU;
        public  GPGPUMode UsingGPGPU { get { return _usingGPGPU; } private set { _usingGPGPU = value; } }

        #endregion

        #region Emitter parameters

        [SerializeField]
        private Mesh[] _meshes = new Mesh[1];
        public  Mesh[]  Meshes { get { return _meshes; } set { _meshes = value; } }

        [SerializeField]
        private Material _emitMaterial;
        public  Material  EmitMaterial { get { return _material; } set { _material = value; } }
        
        [SerializeField]
        private int _emitAmount;
        public  int  EmitAmount { get { return _emitAmount; } set { _emitAmount = value; } }

        [SerializeField]
        private bool _isLoop;
        public  bool  IsLoop { get { return _isLoop; } set { _isLoop = value; } }

        [SerializeField]
        private bool _playOnAwake = true;
        public  bool  PlayOnAwake { get { return _playOnAwake; } set { _playOnAwake = value; } }

        [SerializeField]
        private float _startDelay = 0.0f;
        public  float  StartDelay { get { return _startDelay; } set { _startDelay = value; } }

        [SerializeField]
        private float _duration = 5f;
        public  float  Duration { get { return _duration; } set { _duration = value; } }

        [SerializeField]
        private float _lifeTime = 4.0f;
        public  float  LifeTime { get { return _lifeTime; } set { _lifeTime = value; } }

        [SerializeField, Range(0, 1)]
        private float _lifeTimeRandomize;
        public  float  LifeTimeRandomize { get { return _lifeTimeRandomize; } set { _lifeTimeRandomize = value; } }

        [SerializeField]
        private bool _worldSpace = false;
        public  bool  WorldSpace { get { return _worldSpace; } set { _worldSpace = value; } }

        [SerializeField]
        private Vector3 _emitterPosition = Vector3.zero;
        public  Vector3  EmitterPosition { get { return _emitterPosition; } set { _emitterPosition = value; } }

        [SerializeField]
        private Vector3 _emitterSize = Vector3.one;
        public  Vector3  EmitterSize { get { return _emitterSize; } set { _emitterSize = value; } }

        #endregion

        #region Velocity parameters

        [SerializeField]
        private Vector3 _initialVelocity = Vector3.forward * 4.0f;
        public  Vector3  InitialVelocity { get { return _initialVelocity; } set { _initialVelocity = value; } }

        [SerializeField, Range(0, 1)]
        private float _directionSpread = 0.2f;
        public  float  DirectionSpread { get { return _directionSpread; } set { _directionSpread = value; } }

        [SerializeField, Range(0, 1)]
        private float _speedRandomness = 0.5f;
        public float   SpeedRandomness { get { return _speedRandomness; } set { _speedRandomness = value; } }

        #endregion

        #region Acceleration parameters

        [SerializeField]
        private Vector3 _acceleration = Vector3.zero;
        public  Vector3  Acceleration { get { return _acceleration; } set { _acceleration = value; } }

        [SerializeField, Range(0, 4)]
        private float _drag = 0.1f;
        public  float  Drag { get { return _drag; } set { _drag = value; } }

        #endregion

        #region Rotation parameters

        [SerializeField]
        private float _spin = 20.0f;
        public  float  Spin { get { return _spin; } set { _spin = value; } }

        [SerializeField]
        private float _speedToSpin = 60.0f;
        public  float  SpeedToSpin { get { return _speedToSpin; } set { _speedToSpin = value; } }

        [SerializeField, Range(0, 1)]
        private float _spinRandomness = 0.3f;
        public  float  SpinRandomness { get { return _spinRandomness; } set { _spinRandomness = value; } }

        #endregion

        #region Turbulent noise parameters

        [SerializeField]
        private float _noiseAmplitude = 1.0f;
        public  float  NoiseAmplitude { get { return _noiseAmplitude; } set { _noiseAmplitude = value; } }

        [SerializeField]
        private float _noiseFrequency = 0.2f;
        public  float  NoiseFrequency { get { return _noiseFrequency; } set { _noiseFrequency = value; } }

        [SerializeField]
        private float _noiseMotion = 1.0f;
        public  float  NoiseMotion { get { return _noiseMotion; } set { _noiseMotion = value; } }

        #endregion

        #region Scale parameters

        [SerializeField]
        private float _scale = 1.0f;
        public  float  Scale { get { return _scale; } set { _scale = value; } }

        [SerializeField, Range(0, 1)]
        private float _scaleRandomness = 0.5f;
        public  float  ScaleRandomness { get { return _scaleRandomness; } set { _scaleRandomness = value; } }

        #endregion

        #region Shadow parameters

        [SerializeField]
        private ShadowCastingMode _castShadows;
        public  ShadowCastingMode  CastShadowMode { get { return _castShadows; } set { _castShadows = value; } }

        [SerializeField]
        private bool _receiveShadows = false;
        public  bool  receiveShadows { get { return _receiveShadows; } set { _receiveShadows = value; } }

        #endregion

        #region Topology parameters

        [SerializeField]
        private MeshTopology _meshTopology = MeshTopology.Triangles;
        public  MeshTopology  MeshTopology { get { return _meshTopology; } set { _meshTopology = value; } }

        #endregion

        #region Settings parameters

        [SerializeField]
        private int _randomSeed = 0;
        public  int  randomSeed { get { return _randomSeed; } set { _randomSeed = value; } }

        [SerializeField]
        private bool _isDebug = false;

        #endregion

        #region GPGPU shader

        [SerializeField]
        private ComputeShader _computeShader;

        [SerializeField]
        private Shader _mrtShader;

        [SerializeField]
        private Shader _debugShader;

        #endregion

        #region Compute by compute shader variables

        private enum ComputeKernels
        {
            Initialize,
            Iterator
        }

        private Dictionary<ComputeKernels, int> _kernelMap = new Dictionary<ComputeKernels, int>();
        private ComputeShader _computeInstance;
		private ComputeBuffer _computeRewritableBuffer;

        #endregion

        #region Compute by shader with MRT veriables

        private RenderTexture _positionRT1, _positionRT2;
        private RenderTexture _velocityRT1, _velocityRT2;
        private RenderTexture _rotationRT1, _rotationRT2;
        private Material      _computeMaterial;

        #endregion

        #region veriables

        private Mesh     _batchedMainMesh;
        private Mesh     _batchedFractMesh;
        private Material _material;
        private Material _debugMaterial;

        private Vector3 _emitterPosotion;
        private Vector2 _lifeParams;
        private Vector4 _direction;
        private Vector4 _speedParams;
        private Vector4 _acceleraion;
        private Vector3 _spinParams;
        private Vector3 _noiseOffset = Vector3.zero;

        private int _maxEmitAmountInOneBatch;

        private bool _isStopFromAPI = false;

        private const string _requiredShaderName = "Makibishi";

        static float deltaTime {
            get {
                var isEditor = !Application.isPlaying || Time.frameCount < 2;
                return isEditor ? 1.0f / 10 : Time.deltaTime;
            }
        }

        public int TotalBatches { get; private set; }
        public int MainBatches  { get; private set; }
        public int FractBatch   { get; private set; }

        #endregion


        #region API

        public void Play()
        {
            StopCoroutine(IterateEmit());
            StopCoroutine(IterateDuration());
            if(!_batchedMainMesh)
            {
                BatchMeshes(_meshes, EmitAmount);
            }
            StartCoroutine(IterateEmit());
        }

        public void Stop()
        {
            _isStopFromAPI = true;
        }

        #endregion

        #region Initialize

        void Initialize()
        {
            _material = Instantiate<Material>(_emitMaterial) as Material;
            _material.hideFlags = HideFlags.DontSave;

            BatchMeshes(_meshes, EmitAmount);

            if (UsingGPGPU == GPGPUMode.ComputeShader)
            {
                _computeInstance  = _computeShader;
                _kernelMap = System.Enum.GetValues(typeof(ComputeKernels))
                    .Cast<ComputeKernels>()
                    .ToDictionary(t => t, t => _computeInstance.FindKernel(t.ToString()));
                BuildComputeBuffer();
            }
            else
            {
                _computeMaterial = new Material(_mrtShader);
                _computeMaterial.hideFlags = HideFlags.DontSave;
                BuildMRTBuffer();
                if (_isDebug)
                {
                    _debugMaterial = new Material(_debugShader);
                    _debugMaterial.hideFlags = HideFlags.DontSave;
                }
            }

            if (PlayOnAwake)
            {
                StartCoroutine(IterateEmit());
            }
        }

        void InitialCheck()
        {
            if (!_material.shader.name.StartsWith(_requiredShaderName))
            {
                Debug.LogErrorFormat("Please set the material using {0} shader. asigned shader is {1}", _requiredShaderName, _material.shader.name);
                return;
            }
            if (_emitAmount > MAX_EMIT) _emitAmount = MAX_EMIT;
            if (_usingGPGPU == GPGPUMode.MultipleRenderTarget)
            {
                if (TotalBatches > MAX_TEXTURE)
                {
                    Debug.LogError("Emit amount is too hight");
                    return;
                }
            }
        }

        #endregion

        #region emit and loop

        IEnumerator IterateEmit()
        {
            yield return new WaitForSeconds(_startDelay);
            while(true)
            {
                Emit();
                yield return StartCoroutine(IterateDuration());
                if (_isStopFromAPI)
                {
                    _isStopFromAPI = !_isStopFromAPI;
                    ReleaseMesh();
                    yield break;
                }
                if (!IsLoop)
                {
                    ReleaseMesh();
                    yield break;
                }
                yield return null;
            }
        }

        IEnumerator IterateDuration()
        {
            float timer = 0;

            while(timer < _duration)
            {
                LoopEmit();
                timer += deltaTime;
                if(!IsLoop && timer > _lifeTime)
                {
                    yield break;
                }
                if(_isStopFromAPI)
                {
                    yield break;
                }
                yield return null;
            }
        }

        void Emit()
        {
            if (UsingGPGPU == GPGPUMode.ComputeShader)
            {
                InitializeComputeShader();
                DrawMesh();
            }
            else
            {
                InitializeMRTShader();
                DrawMesh();
            }
        }

        void LoopEmit()
        {
            if (UsingGPGPU == GPGPUMode.ComputeShader)
            {
                UpdateComputeShader();
                DrawMesh();
            }
            else
            {
                UpdateMRTShader();
                DrawMesh();
            }
        }

        #endregion

        #region make paramaters

        void UpdateBufferParams()
        {
            if (_worldSpace) _emitterPosition = transform.position;
            float invLifeMax = 1.0f / Mathf.Max(LifeTime, 0.01f);
            float invLifeMin = invLifeMax / Mathf.Max(1 - LifeTimeRandomize, 0.01f);
            _lifeParams = new Vector2(invLifeMin, invLifeMax);

            if (_initialVelocity == Vector3.zero)
            {
                _direction = new Vector4(0, 0, 1, 0);
                _speedParams = Vector4.zero;
            }
            else
            {
                var speed = _initialVelocity.magnitude;
                var dir   = _initialVelocity / speed;
                _direction   = new Vector4(dir.x, dir.y, dir.z, _directionSpread);
                _speedParams = new Vector2(speed, _speedRandomness);
            }

            var drag = Mathf.Exp(-_drag * deltaTime);
            _acceleraion = new Vector4(_acceleration.x, _acceleration.y, _acceleration.z, drag);

            var pi360 = Mathf.PI / 360;
            _spinParams = new Vector3(_spin * pi360, _speedToSpin * pi360, _spinRandomness);

            if (_acceleration == Vector3.zero)
                _noiseOffset += Vector3.up * _noiseMotion * deltaTime;
            else
                _noiseOffset += _acceleration.normalized * _noiseMotion * deltaTime;
        }

        #endregion

        #region compute shader process

        void BuildComputeBuffer()
        {
			_computeRewritableBuffer = new ComputeBuffer(_emitAmount, Marshal.SizeOf(typeof(RewritableBuffer)));
			RewritableBuffer[] rewritableBuffers = new RewritableBuffer[_computeRewritableBuffer.count];
			_computeRewritableBuffer.SetData(rewritableBuffers);
            _material.EnableKeyword("_USE_COMPUTESHADER");
        }

        void SendPropBuffersToComputeShader()
        {
            _computeInstance.SetVector("emitterPos", _emitterPosition);
            _computeInstance.SetVector("emitterSize", _emitterSize);
            _computeInstance.SetVector("lifeParams", _lifeParams);
            _computeInstance.SetVector("direction", _direction);
            _computeInstance.SetVector("speedParams", _speedParams);
            _computeInstance.SetVector("acceleration", _acceleraion);
            _computeInstance.SetVector("spinParams", _spinParams);
            _computeInstance.SetVector("noiseParams", new Vector2(_noiseFrequency, _noiseAmplitude));
            _computeInstance.SetVector("noiseOffset", _noiseOffset);
            _computeInstance.SetVector("config", new Vector4(_isLoop ? 0f : 1f, _randomSeed, deltaTime, Time.time));
        }

        void InitializeComputeShader()
        {
            _noiseOffset = Vector3.zero;
            UpdateBufferParams();
            SendPropBuffersToComputeShader();

            _computeInstance.SetInt("requiredThread", _emitAmount);
            _computeInstance.SetBuffer(_kernelMap[ComputeKernels.Initialize], "_rwBuf", _computeRewritableBuffer);
            _computeInstance.Dispatch(_kernelMap[ComputeKernels.Initialize], (_computeRewritableBuffer.count + THREAD_GROUP - 1) / THREAD_GROUP, 1, 1);
            _material.SetBuffer("_rwBuf", _computeRewritableBuffer);
            if (_worldSpace)
                _material.EnableKeyword("_WORLDSPACE");
            else
                _material.DisableKeyword("_WORLDSPACE");
        }

        void UpdateComputeShader()
        {
            UpdateBufferParams();
            SendPropBuffersToComputeShader();

            _computeInstance.SetInt("requiredThread", _emitAmount);
            _computeInstance.SetBuffer(_kernelMap[ComputeKernels.Iterator], "_rwBuf", _computeRewritableBuffer);
            _computeInstance.Dispatch(_kernelMap[ComputeKernels.Iterator], (_computeRewritableBuffer.count + THREAD_GROUP - 1) / THREAD_GROUP, 1, 1);
            _material.SetBuffer("_rwBuf", _computeRewritableBuffer);
            if (_worldSpace)
                _material.EnableKeyword("_WORLDSPACE");
            else
                _material.DisableKeyword("_WORLDSPACE");
        }

        #endregion

        #region multiple render target process

        RenderTexture CreateRT()
        {
            var width  = _maxEmitAmountInOneBatch;
            var height = TotalBatches;
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
            rt.hideFlags = HideFlags.DontSave;
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Repeat;
            return rt;
        }

        void SendPropBuffersToMRTShader()
        {
            _computeMaterial.SetVector("emitterPos", _emitterPosition);
            _computeMaterial.SetVector("emitterSize", _emitterSize);
            _computeMaterial.SetVector("lifeParams", _lifeParams);
            _computeMaterial.SetVector("direction", _direction);
            _computeMaterial.SetVector("speedParams", _speedParams);
            _computeMaterial.SetVector("acceleration", _acceleraion);
            _computeMaterial.SetVector("spinParams", _spinParams);
            _computeMaterial.SetVector("noiseParams", new Vector2(_noiseFrequency, _noiseAmplitude));
            _computeMaterial.SetVector("noiseOffset", _noiseOffset);
            _computeMaterial.SetVector("config", new Vector4(_isLoop ? 0f : 1f, _randomSeed, deltaTime, Time.time));
        }

        void BuildMRTBuffer()
        {
            if (_positionRT1) DestroyImmediate(_positionRT1);
            if (_positionRT2) DestroyImmediate(_positionRT2);
            if (_velocityRT1) DestroyImmediate(_velocityRT1);
            if (_velocityRT2) DestroyImmediate(_velocityRT2);
            if (_rotationRT1) DestroyImmediate(_rotationRT1);
            if (_rotationRT2) DestroyImmediate(_rotationRT2);

            _positionRT1 = CreateRT();
            _positionRT2 = CreateRT();
            _velocityRT1 = CreateRT();
            _velocityRT2 = CreateRT();
            _rotationRT1 = CreateRT();
            _rotationRT2 = CreateRT();

            _material.DisableKeyword("_USE_COMPUTESHADER");
        }

        void InitializeMRTShader()
        {
            _noiseOffset = Vector3.zero;
            UpdateBufferParams();
            SendPropBuffersToMRTShader();

            Graphics.Blit(null, _positionRT1, _computeMaterial, 0);
            Graphics.Blit(null, _velocityRT1, _computeMaterial, 1);
            Graphics.Blit(null, _rotationRT1, _computeMaterial, 2);

            for (var i = 0; i < 8; i++) {
                UpdateMRTShader();
                UpdateBufferParams();
                SendPropBuffersToMRTShader();
            }

            if (_worldSpace)
                _material.EnableKeyword("_WORLDSPACE");
            else
                _material.DisableKeyword("_WORLDSPACE");
        }

        void UpdateMRTShader()
        {
            UpdateBufferParams();
            SendPropBuffersToMRTShader();

            SwapRT(ref _positionRT1, ref _positionRT2);
            SwapRT(ref _velocityRT1, ref _velocityRT2);
            SwapRT(ref _rotationRT1, ref _rotationRT2);

            _computeMaterial.SetTexture("_positionBuffer", _positionRT2);
            _computeMaterial.SetTexture("_VelocityBuffer", _velocityRT2);
            _computeMaterial.SetTexture("_RotationBuffer", _rotationRT2);

            Graphics.Blit(null, _positionRT1, _computeMaterial, 3);
            _computeMaterial.SetTexture("_PositionBuffer", _positionRT1);
            Graphics.Blit(null, _velocityRT1, _computeMaterial, 4);
            Graphics.Blit(null, _rotationRT1, _computeMaterial, 5);

            if (_worldSpace)
                _material.EnableKeyword("_WORLDSPACE");
            else
                _material.DisableKeyword("_WORLDSPACE");
        }

        void SwapRT(ref RenderTexture rt1, ref RenderTexture rt2)
        {
            var temp = rt2;
            rt2      = rt1;
            rt1      = temp;
        }

        #endregion

        #region batch and draw mesh

        void BatchMeshes(Mesh[] meshes, int amount)
        {
            if (meshes == null || meshes.Length == 0 || amount == 0)
                return;

            int oneEmitUnitVertices = 0;
            int oneEmitUnitIndices  = 0;
            int oneEmitUnitTangents = 0;
            int mainMeshVertices    = 0;
            int mainMeshIndices     = 0;
            int fractMeshVertices   = 0;
            int fractMeshIndices    = 0;

            bool hasTangent = false;

            foreach (Mesh m in meshes)
            {
                oneEmitUnitVertices += m.vertices.Length;
                oneEmitUnitIndices  += m.GetIndices(0).Length;
                oneEmitUnitTangents += m.tangents.Length;
            }

            if (oneEmitUnitVertices == 0) return;
            if (oneEmitUnitVertices > MAX_VERTICES)
            {
                Debug.LogWarningFormat("Emit mesh vertexcount {0} is too lerge, reduce under {1}", oneEmitUnitVertices, MAX_VERTICES);
                return;
            }
            if (oneEmitUnitTangents == oneEmitUnitVertices) hasTangent = true;

            _maxEmitAmountInOneBatch = Mathf.FloorToInt(MAX_VERTICES / oneEmitUnitVertices) * meshes.Length;
            if (_usingGPGPU == GPGPUMode.MultipleRenderTarget && _maxEmitAmountInOneBatch > MAX_TEXTURE)
                _maxEmitAmountInOneBatch = MAX_TEXTURE - (MAX_TEXTURE % meshes.Length);

            FractBatch = (_emitAmount > _maxEmitAmountInOneBatch && (float)_emitAmount % (float)_maxEmitAmountInOneBatch != 0)? 1:0;

            if (FractBatch == 1)
            {
                MainBatches  = Mathf.FloorToInt((float)_emitAmount / (float)_maxEmitAmountInOneBatch);
                TotalBatches = MainBatches + 1;

                for (int i = 0; i < _emitAmount - (MainBatches - 1) * _maxEmitAmountInOneBatch; i++)
                {
                    var sourceMesh = meshes[i % meshes.Length];
                    var vertCount = sourceMesh.vertexCount;
                    var indexCount = sourceMesh.GetIndices(0).Length;
                    if (i < _maxEmitAmountInOneBatch)
                    {
                        mainMeshVertices += vertCount;
                        mainMeshIndices  += indexCount;
                    }
                    else
                    {
                        fractMeshVertices += vertCount;
                        fractMeshIndices  += indexCount;
                    }
                }
                #if UNITY_EDITOR
                Debug.LogFormat("One Emit Unit vertices is {0}, Max Emit Amount In One Batch is {1}", oneEmitUnitVertices , _maxEmitAmountInOneBatch);
                Debug.LogFormat("MainBatch info, MainMeshVertices is {0}, MainMeshIndexes is {1}, MainBachCount is {2}", mainMeshVertices, mainMeshIndices, MainBatches);
                Debug.LogFormat("FractBatch info, FracMeshtVertices is {0}, FractMeshIndexes is {1}, Run {2} fractBach", fractMeshVertices, fractMeshIndices, FractBatch);
                #endif
            }
            else
            {
                MainBatches  = Mathf.CeilToInt((float)_emitAmount / (float)_maxEmitAmountInOneBatch);
                TotalBatches = MainBatches;

                for (int i = 0; i < _emitAmount - (MainBatches - 1) * _maxEmitAmountInOneBatch; i++)
                {
                    var sourceMesh = meshes[i % meshes.Length];
                    var vertCount = sourceMesh.vertexCount;
                    var indexCount = sourceMesh.GetIndices(0).Length;
                    mainMeshVertices += vertCount;
                    mainMeshIndices  += indexCount;
                }
                #if UNITY_EDITOR
                Debug.LogFormat("One Emit Unit vertices is {0}, Max Emit Amount In One Batch is {1}", oneEmitUnitVertices , _maxEmitAmountInOneBatch);
                Debug.LogFormat("Mainbatch info, MainMeshVertices is {0}, MainBachCount is {1} and does't use fractBatch",mainMeshVertices,TotalBatches);
                #endif
            }

            var mainVertices = new Vector3[mainMeshVertices];
            var mainNormals  = new Vector3[mainMeshVertices];
            var mainTangents = new Vector4[mainMeshVertices];
            var mainUV       = new Vector2[mainMeshVertices];
            var mainUV2      = new Vector2[mainMeshVertices];
            var mainIndicies = new int[mainMeshIndices];

            for (int vertexOffset = 0, indexOffset = 0, i = 0; vertexOffset < mainMeshVertices; i++)
            {
                var sourceMesh  = meshes[i % meshes.Length];
                var sourceIndex = sourceMesh.GetIndices(0);

                Array.Copy(sourceMesh.vertices, 0, mainVertices, vertexOffset, sourceMesh.vertices.Length);
                Array.Copy(sourceMesh.normals,  0,  mainNormals, vertexOffset, sourceMesh.vertices.Length);
                Array.Copy(sourceMesh.uv,       0,       mainUV, vertexOffset, sourceMesh.vertices.Length);
                if (hasTangent) Array.Copy(sourceMesh.tangents, 0, mainTangents, vertexOffset, sourceMesh.vertices.Length);

                for (int j = 0; j < sourceIndex.Length; j++)
                {
                    mainIndicies[indexOffset + j] = vertexOffset + sourceIndex[j];
                }

                if(_usingGPGPU == GPGPUMode.ComputeShader)
                {
                    for (int k = 0; k < sourceMesh.vertexCount; k++)
                    {
                        mainUV2[vertexOffset + k] = new Vector2((float)i, 0f);
                    }
                }
                else
                {
                    var coord = new Vector2((float)i / (float)_maxEmitAmountInOneBatch, 0f);
                    for (var k = 0; k < sourceMesh.vertexCount; k++)
                    {
                        mainUV2[vertexOffset + k] = coord;
                    }
                }

                vertexOffset += sourceMesh.vertexCount;
                indexOffset  += sourceIndex.Length;
            }

            _batchedMainMesh = new Mesh();

            _batchedMainMesh.vertices = mainVertices;
            _batchedMainMesh.normals  = mainNormals;
            _batchedMainMesh.uv       = mainUV;
            _batchedMainMesh.uv2      = mainUV2;
            if (hasTangent) _batchedMainMesh.tangents = mainTangents;

            _batchedMainMesh.SetIndices(mainIndicies, _meshTopology, 0);
            _batchedMainMesh.Optimize();

            _batchedMainMesh.hideFlags = HideFlags.DontSave;

            _batchedMainMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);

            if(FractBatch == 1)
            {
                var fractVertices = new Vector3[fractMeshVertices];
                var fractNormals  = new Vector3[fractMeshVertices];
                var fractTangents = new Vector4[fractMeshVertices];
                var fractUV       = new Vector2[fractMeshVertices];
                var fractUV2      = new Vector2[fractMeshVertices];
                var fractIndicies = new int[fractMeshIndices];

                for (int vertexOffset = 0, indexOffset = 0, i = 0; vertexOffset < fractMeshVertices; i++)
                {
                    var sourceMesh  = meshes[i % meshes.Length];
                    var sourceIndex = sourceMesh.GetIndices(0);

                    Array.Copy(sourceMesh.vertices, 0, fractVertices, vertexOffset, sourceMesh.vertices.Length);
                    Array.Copy(sourceMesh.normals, 0, fractNormals, vertexOffset, sourceMesh.vertices.Length);
                    Array.Copy(sourceMesh.uv, 0, fractUV, vertexOffset, sourceMesh.vertices.Length);
                    if (hasTangent) Array.Copy(sourceMesh.tangents, 0, fractTangents, vertexOffset, sourceMesh.vertices.Length);

                    for (int j = 0; j < sourceIndex.Length; j++)
                    {
                        fractIndicies[indexOffset + j] = vertexOffset + sourceIndex[j];
                    }

                    if (_usingGPGPU == GPGPUMode.ComputeShader)
                    {
                        for (int k = 0; k < sourceMesh.vertexCount; k++)
                        {
                            fractUV2[vertexOffset + k] = new Vector2((float)i, 0f);
                        }
                    }
                    else
                    {
                        var coord = new Vector2((float)i / (float)_maxEmitAmountInOneBatch, 0f);
                        for (var k = 0; k < sourceMesh.vertexCount; k++)
                        {
                            fractUV2[vertexOffset + k] = coord;
                        }
                    }

                    vertexOffset += sourceMesh.vertexCount;
                    indexOffset += sourceIndex.Length;
                }

                _batchedFractMesh = new Mesh();

                _batchedFractMesh.vertices = fractVertices;
                _batchedFractMesh.normals = fractNormals;
                _batchedFractMesh.uv = fractUV;
                _batchedFractMesh.uv2 = fractUV2;
                if (hasTangent) _batchedFractMesh.tangents = fractTangents;

                _batchedFractMesh.SetIndices(fractIndicies, _meshTopology, 0);
                _batchedFractMesh.Optimize();

                _batchedFractMesh.hideFlags = HideFlags.DontSave;

                _batchedFractMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            }
        }

        void DrawMesh()
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetFloat("_ScaleMin", _scale * (1 - _scaleRandomness));
            propBlock.SetFloat("_ScaleMax", _scale);
            propBlock.SetFloat("_RandomSeed", _randomSeed);
            Vector2 bufferOffset = Vector2.zero;
            if (_usingGPGPU == GPGPUMode.MultipleRenderTarget)
            {
                propBlock.SetTexture("_PositionBuffer", _positionRT1);
                propBlock.SetTexture("_RotationBuffer", _rotationRT1);
                bufferOffset = new Vector2(0.5f / (float)_positionRT1.width, 0);
            }

            for (int i = 0; i < MainBatches; i++)
            {
                if (_usingGPGPU == GPGPUMode.ComputeShader)
                {
                    propBlock.SetFloat("_EmitIdOffset", i * _maxEmitAmountInOneBatch);
                }
                else
                {
                    bufferOffset.y = (0.5f + i) / (float)_positionRT1.height;
                    propBlock.SetVector("_BufferOffset", bufferOffset);
                }

                Graphics.DrawMesh(
                    _batchedMainMesh,
                    transform.position,
                    transform.rotation,
                    _material,
                    0,
                    null,
                    0,
                    propBlock,
                    _castShadows,
                    _receiveShadows
                    );
            }

            if (FractBatch != 0)
            {
                propBlock.SetFloat("_EmitIdOffset", MainBatches * _maxEmitAmountInOneBatch);
                if (_usingGPGPU == GPGPUMode.MultipleRenderTarget)
                {
                    bufferOffset.y = (0.5f + (float)MainBatches) / (float)_positionRT1.height;
                    propBlock.SetVector("_BufferOffset", bufferOffset);
                }
                Graphics.DrawMesh(
                    _batchedFractMesh,
                    transform.position,
                    transform.rotation,
                    _material,
                    0,
                    null,
                    0,
                    propBlock,
                    _castShadows,
                    _receiveShadows
                    );
            }
        }

        #endregion

        #region release

        void ReleaseMesh()
        {
            if (_batchedMainMesh)
            {
                DestroyImmediate(_batchedMainMesh);
            }
            if (_batchedFractMesh)
            {
                DestroyImmediate(_batchedFractMesh);
            }
        }

        void CleanUp()
        {
            if (_usingGPGPU == GPGPUMode.ComputeShader)
            {
                if (_computeRewritableBuffer != null) _computeRewritableBuffer.Release();
            }
            else
            {
                if (_positionRT1) DestroyImmediate(_positionRT1);
                if (_positionRT2) DestroyImmediate(_positionRT2);
                if (_velocityRT1) DestroyImmediate(_velocityRT1);
                if (_velocityRT2) DestroyImmediate(_velocityRT2);
                if (_rotationRT1) DestroyImmediate(_rotationRT1);
                if (_rotationRT2) DestroyImmediate(_rotationRT2);
                if (_computeMaterial) DestroyImmediate(_computeMaterial);
            }
            if (_material != null)
            {
                DestroyImmediate(_material);
            }
            StopAllCoroutines();
            ReleaseMesh();
            #if UNITY_EDITOR
            Debug.Log("Buffer released");
            #endif
        }

        #endregion

        #region unity builtin

        void OnDestroy()
        {
            CleanUp();
        }

        void Start()
        {
            Initialize();
            InitialCheck();
        }

        #endregion

        #region debug

        #if UNITY_EDITOR
        void OnGUI()
        {
            if (_isDebug)
            {
                if (GUI.Button(new Rect(10, 10, 50, 20), "Play"))
                {
                    Play();
                }
                if (GUI.Button(new Rect(10, 35, 50, 20), "Stop"))
                {
                    Stop();
                }
            }
            if (_debugMaterial && _positionRT2 && _velocityRT2 && _rotationRT2)
                {
                    var w = _positionRT2.width;
                    var h = _positionRT2.height;

                    var rect = new Rect(0, 0, w, h);
                    Graphics.DrawTexture(rect, _positionRT2, _debugMaterial);

                    rect.y += h;
                    Graphics.DrawTexture(rect, _velocityRT2, _debugMaterial);

                    rect.y += h;
                    Graphics.DrawTexture(rect, _rotationRT2, _debugMaterial);
                }
        }
        #endif

        #endregion
    }
}