using UnityEngine;

/// <summary>
/// Lets you select from the project's tag with a dropdown for string fields.<br/><br/>
/// Original by DYLAN ENGELMAN <see href="http://jupiterlighthousestudio.com/custom-inspectors-unity/"/><br/>
/// Altered by Brecht Lecluyse <see href="http://www.brechtos.com"/><br/>
/// Found via <see href="http://www.brechtos.com/tagselectorattribute/"/><br/>
/// </summary>
public class TagSelectorAttribute : PropertyAttribute
{
    public bool UseDefaultTagFieldDrawer = false;
}