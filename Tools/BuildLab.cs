public static class BuildLab
{
    public static string Main()
    {
        var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (current.isDirty)
        {
            var path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeBallisticSetup.unity");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(current, path, true))
                throw new System.InvalidOperationException("No se pudo preservar la escena actual.");
        }
        Ballistics.Editor.BallisticSceneBuilder.Build();
        return Ballistics.Editor.BallisticSceneBuilder.ScenePath;
    }
}
