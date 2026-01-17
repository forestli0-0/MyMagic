using UnityEngine;

namespace CombatSystem.Data
{
    /// <summary>
    /// 关卡定义，包含场景名称与出生点元数据。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Levels/Level Definition", fileName = "Level_")]
    public class LevelDefinition : DefinitionBase
    {
        [Header("场景")]
        [SerializeField] private string sceneName;

#if UNITY_EDITOR
        [SerializeField] private UnityEditor.SceneAsset sceneAsset;
#endif

        [Header("出生点")]
        [SerializeField] private string defaultSpawnId = "Start";
        [SerializeField] private string returnSpawnId = "Return";

        public string SceneName => sceneName;
        public string DefaultSpawnId => defaultSpawnId;
        public string ReturnSpawnId => returnSpawnId;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (sceneAsset != null)
            {
                sceneName = sceneAsset.name;
            }
        }
#endif
    }
}
