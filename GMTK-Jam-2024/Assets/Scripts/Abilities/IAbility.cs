using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAbility
{
    /// <summary>
    /// Initiate this object's ability. Triggered when said object is possessed and the player hits the ability button.
    /// </summary>
    void DoAbility();
}
