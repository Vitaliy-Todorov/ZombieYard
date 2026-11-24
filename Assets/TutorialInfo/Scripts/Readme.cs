// Ответственности
// - Хранит текст, иллюстрацию и ссылки стартовой памятки Unity.

using System;
using UnityEngine;

public class Readme : ScriptableObject
{
    public Texture2D icon;
    public string title;
    public Section[] sections;
    public bool loadedLayout;

    [Serializable]
    public class Section
    {
        public string heading, text, linkText, url;
    }
}
