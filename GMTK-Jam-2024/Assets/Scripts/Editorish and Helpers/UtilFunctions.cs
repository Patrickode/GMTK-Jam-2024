using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class UtilFunctions
{
#if UNITY_EDITOR
    /// <summary>
    /// Prints the properties of a serialized object to the console using <see cref="Debug.Log(object)"/>.
    /// </summary>
    /// <param name="maxDepth">How many levels of child properties to print. 0 means no child properties.</param>
    public static void PrintProperties(this UnityEditor.SerializedObject propertyOwner, int maxDepth = int.MaxValue)
    {
        //no negativity allowed
        maxDepth = Mathf.Max(maxDepth, 0);

        var propertyIterator = propertyOwner.GetIterator();
        if (propertyIterator == null)
        {
            Debug.LogWarning($"Object {propertyOwner} has no properties.");
            return;
        }

        propertyIterator.PrintProperties(maxDepth);
    }
    /// <summary>
    /// Prints the child properties of a serialized property to the console using <see cref="Debug.Log(object)"/>.
    /// </summary>
    /// <inheritdoc cref="PrintProperties(UnityEditor.SerializedObject, int)"/>
    public static void PrintProperties(this UnityEditor.SerializedProperty property, int maxDepth = int.MaxValue)
    {
        //no negativity allowed
        maxDepth = Mathf.Max(maxDepth, 0);

        if (!property.hasChildren)
        {
            Debug.LogWarning($"Property {property.name} has no child properties.");
            return;
        }

        property.Next(true);
        Debug.Log(">\t" + property.name + "\n");
        int initDepth = property.depth;

        string indent;
        var currentDepth = 0;
        while (property.Next(currentDepth < maxDepth))
        {
            currentDepth = property.depth - initDepth;
            if (currentDepth < 0) break;

            //Indent equal to depth, and put a ">" before the last tab.
            indent = (currentDepth == 0 ? ">" : "") + "\t";
            for (int i = 0; i < currentDepth - 1; i++) indent += "\t";
            if (currentDepth > 0) indent += ">\t";

            Debug.Log(indent + property.name + "\n");
        }
    }

    /// <summary>
    /// Clears the log of <see cref="Debug.Log(object)"/> messages and similar.<br/><br/>
    /// Sourced from <see href="https://stackoverflow.com/a/40578161"/>.<br/>
    /// <b>CAUTION: This method uses reflection,</b> so if Unity changes <br/>the names of anything, 
    /// this method will stop working.
    /// </summary>
    public static void ClearEditorLog()
    {
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
    }
#endif

    /// <summary>
    /// Gets the renderers of <paramref name="obj"/> and its children, and returns the combined bounds of 
    /// all the active/enabled ones.<br/>
    /// Note this returns <i><b>rendered</b></i> bounds, not the bounds of the object's collider.
    /// </summary>
    /// <remarks>
    /// <b>Developer's Note:</b> Renderer bounds tend to include transparent fringes, i.e. padding. See<br/>
    /// <see cref="UnityEngine.Sprites.DataUtility.GetPadding(Sprite)"/>.
    /// </remarks>
    /// <param name="obj">The object to get the total rendered bounds of.</param>
    /// <returns>A <see cref="Bounds"/> that encapsulates the all the active/enabled renderers of 
    /// <paramref name="obj"/> and its children.</returns>
    public static Bounds GetTotalRenderedBounds(this GameObject obj)
    {
        //If this parent obj isn't active, none of the children will be either, so we can return early.
        if (!obj.activeInHierarchy)
            return new Bounds(obj.transform.position, Vector3.zero);

        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        return GetTotalRenderedBoundsNonAlloc(rends, obj.transform.position);
    }
    /// <summary>
    /// Returns the combined <see cref="Bounds"/> of all the active/enabled renderers in <paramref name="rends"/>.
    /// </summary>
    /// <param name="rends">The renderers to get the total rendered <see cref="Bounds"/> of.</param>
    /// <param name="defaultCenter">The default center position to use for zero-sized <see cref="Bounds"/>/failures.</param>
    /// <returns></returns> <inheritdoc cref="GetTotalRenderedBounds(GameObject)"/>
    public static Bounds GetTotalRenderedBoundsNonAlloc(Renderer[] rends, Vector3 defaultCenter = default)
    {
        //If there are no renderers, there's no rendered bounds. Return a zero-sized bounds.
        if (rends == null || rends.Length < 1)
            return new Bounds(defaultCenter, Vector3.zero);

        //Init a bounds with the first enabled/active renderer, then expand it to include all the others that're
        //enabled/active.
        Bounds? totalBounds = null;
        foreach (Renderer rend in rends)
            if (rend.enabled && rend.gameObject.activeInHierarchy)
            {
                if (totalBounds is Bounds tBounds)
                    tBounds.Encapsulate(rend.bounds);
                else
                    totalBounds = rend.bounds;
            }

        //If we didn't get to init a bounds, that means none of the renderers are actually showing. In that case,
        //return a zero-sized bounds.
        return totalBounds is Bounds result
            ? result
            : new Bounds(defaultCenter, Vector3.zero);
    }

    /// <summary>
    /// Counts the bounding box corners of the given RectTransform that are visible from the given Camera in screen space.
    /// </summary>
    /// <returns>The amount of bounding box corners that are visible from the Camera.</returns>
    /// <remarks>Sourced from <see href="https://forum.unity3d.com/threads/test-if-ui-element-is-visible-on-screen.276549/#post-2978773"/>.</remarks>
    private static int CountCornersVisibleFrom(this RectTransform rectTransform, Camera camera)
    {
        Rect screenBounds = new Rect(0f, 0f, Screen.width, Screen.height); // Screen space bounds (assumes camera renders across the entire screen)
        Vector3[] objectCorners = new Vector3[4];
        rectTransform.GetWorldCorners(objectCorners);

        int visibleCorners = 0;
        Vector3 tempScreenSpaceCorner; // Cached
        for (var i = 0; i < objectCorners.Length; i++) // For each corner in rectTransform
        {
            tempScreenSpaceCorner = camera.WorldToScreenPoint(objectCorners[i]); // Transform world space position of corner to screen space
            if (screenBounds.Contains(tempScreenSpaceCorner)) // If the corner is inside the screen
            {
                visibleCorners++;
            }
        }
        return visibleCorners;
    }

    /// <summary>
    /// Determines if this RectTransform is fully visible from the specified camera.<br/>
    /// Works by checking if each bounding box corner of this RectTransform is inside the cameras screen space view frustrum.
    /// </summary>
    /// <returns><c>true</c> if is fully visible from the specified camera; otherwise, <c>false</c>.</returns>
    /// <inheritdoc cref="CountCornersVisibleFrom(RectTransform, Camera)"/>
    public static bool IsFullyVisibleFrom(this RectTransform rectTransform, Camera camera)
    {
        return CountCornersVisibleFrom(rectTransform, camera) == 4; // True if all 4 corners are visible
    }

    /// <summary>
    /// Determines if this RectTransform is at least partially visible from the specified camera.<br/>
    /// Works by checking if any bounding box corner of this RectTransform is inside the cameras screen space view frustrum.
    /// </summary>
    /// <returns><c>true</c> if is at least partially visible from the specified camera; otherwise, <c>false</c>.</returns>
    /// <inheritdoc cref="CountCornersVisibleFrom(RectTransform, Camera)"/>
    public static bool IsVisibleFrom(this RectTransform rectTransform, Camera camera)
    {
        return CountCornersVisibleFrom(rectTransform, camera) > 0; // True if any corners are visible
    }

    public static Bounds EncapsulateAll(params Bounds[] bounds)
    {
        if (bounds.Length < 1)
            return new Bounds(Vector3.zero, Vector3.zero);

        Bounds result = bounds[0];
        for (int i = 1; i < bounds.Length; i++)
            result.Encapsulate(bounds[i]);

        return result;
    }

    /// <summary>
    /// Uses bit operators to determine if a given layer is in this layer mask.<br/>
    /// Sourced from <see href="https://forum.unity.com/threads/checking-if-a-layer-is-in-a-layer-mask.1190230/#post-7613611"/>.
    public static bool Includes(this LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;

    public static float GetSurfaceArea(this Bounds bounds)
        => 2 * bounds.size.x * bounds.size.y + 2 * bounds.size.x * bounds.size.z + 2 * bounds.size.y * bounds.size.z;

    #region Draw Box/Bounds | Lightly modified from unitycoder via https://gist.github.com/unitycoder/58f4b5d80f423d29e35c814a9556f9d9
    public static void DrawBounds(Bounds b, Color c = default, float duration = 0)
    {
        // bottom, counter-clockwise from back bottom left
        var p1 = new Vector3(b.min.x, b.min.y, b.min.z);    //---
        var p2 = new Vector3(b.max.x, b.min.y, b.min.z);    //+--
        var p3 = new Vector3(b.max.x, b.min.y, b.max.z);    //+-+
        var p4 = new Vector3(b.min.x, b.min.y, b.max.z);    //--+

        Debug.DrawLine(p1, p2, c, duration);
        Debug.DrawLine(p2, p3, c, duration);
        Debug.DrawLine(p3, p4, c, duration);
        Debug.DrawLine(p4, p1, c, duration);

        // top, counter-clockwise from back top left
        var p5 = new Vector3(b.min.x, b.max.y, b.min.z);    //-+-
        var p6 = new Vector3(b.max.x, b.max.y, b.min.z);    //++-
        var p7 = new Vector3(b.max.x, b.max.y, b.max.z);    //+++
        var p8 = new Vector3(b.min.x, b.max.y, b.max.z);    //-++

        Debug.DrawLine(p5, p6, c, duration);
        Debug.DrawLine(p6, p7, c, duration);
        Debug.DrawLine(p7, p8, c, duration);
        Debug.DrawLine(p8, p5, c, duration);

        // sides, counter-clockwise from back left
        Debug.DrawLine(p1, p5, c, duration);
        Debug.DrawLine(p2, p6, c, duration);
        Debug.DrawLine(p3, p7, c, duration);
        Debug.DrawLine(p4, p8, c, duration);
    }

    //Comment included with function:
    // https://forum.unity.com/threads/debug-drawbox-function-is-direly-needed.1038499/
    public static void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c = default, float duration = 0, bool drawAxes = false)
    {
        Matrix4x4 m = new Matrix4x4();
        m.SetTRS(pos, rot, scale);

        //bottom, counter-clockwise from front bottom left
        var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));  //--+
        var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));   //+-+
        var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));  //+--
        var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f)); //---

        Debug.DrawLine(point1, point2, c, duration);
        Debug.DrawLine(point2, point3, c, duration);
        Debug.DrawLine(point3, point4, c, duration);
        Debug.DrawLine(point4, point1, c, duration);

        //top, counter-clockwise from front top left
        var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));   //-++
        var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));    //+++
        var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));   //++-
        var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));  //-+-

        Debug.DrawLine(point5, point6, c, duration);
        Debug.DrawLine(point6, point7, c, duration);
        Debug.DrawLine(point7, point8, c, duration);
        Debug.DrawLine(point8, point5, c, duration);

        //corners, counter-clockwise from front left
        Debug.DrawLine(point1, point5, c, duration);
        Debug.DrawLine(point2, point6, c, duration);
        Debug.DrawLine(point3, point7, c, duration);
        Debug.DrawLine(point4, point8, c, duration);

        // optional axis display (original code causes compiler errors; Matrix4x4s don't have definitions for the methods used)
        if (drawAxes)
        {
            Color halfSatC = c - Color.HSVToRGB(0, c.ToHSV().y / 2, 0);
            Debug.DrawRay(pos, rot * (Vector3.forward * scale.x / 2), halfSatC);
            Debug.DrawRay(pos, rot * (Vector3.up * scale.x / 2), halfSatC);
            Debug.DrawRay(pos, rot * (Vector3.right * scale.x / 2), halfSatC);
        }
    }
    public static void DrawBox(Vector3 pos, Quaternion rot, float scale, Color c = default, float duration = 0, bool drawAxes = false)
        => DrawBox(pos, rot, Vector3.one * scale, c, duration, drawAxes);
    #endregion

    /// <summary>
    /// Draws a sphere out of lines using <see cref="Debug.DrawLine(Vector3, Vector3, Color)"/>.<br/>
    /// Taken from <br/>
    /// <see href="https://github.com/Unity-Technologies/Graphics/pull/2287/files#diff-cc2ed84f51a3297faff7fd239fe421ca1ca75b9643a22f7808d3a274ff3252e9R195"/>,
    /// <br/>which was found via <see href="https://forum.unity.com/goto/post?id=8098139#post-8098139"/>.
    /// </summary>
    public static void DrawSphere(Vector3 pos, float radius,
        Color color = default, float duration = 0f)
    {
        Vector3[] v = _cacheUnitSphere;
        int len = v.Length / 3;

        for (int i = 0; i < len; i++)
        {
            var startX = pos + radius * v[0 * len + i];
            var endX = pos + radius * v[0 * len + (i + 1) % len];
            var startY = pos + radius * v[1 * len + i];
            var endY = pos + radius * v[1 * len + (i + 1) % len];
            var startZ = pos + radius * v[2 * len + i];
            var endZ = pos + radius * v[2 * len + (i + 1) % len];
            Debug.DrawLine(startX, endX, color, duration);
            Debug.DrawLine(startY, endY, color, duration);
            Debug.DrawLine(startZ, endZ, color, duration);
        }
    }
    /// <summary>
    /// Draws a sphere out of lines using <see cref="Debug.DrawLine(Vector3, Vector3, Color)"/>.<br/>
    /// This overload draws <paramref name="horzDivisions"/> z-plane circles rotated around the z-axis,
    /// and y-plane circles rotated around the y axis (as opposed<br/> to 
    /// <see cref="DrawSphere(Vector3, float, Color, float)"/>'s three lines along each axis plane).
    /// </summary>
    public static void DrawSphere(Vector3 pos, float radius, int horzDivisions, int vertDivisions,
        Color horzColor = default, Color vertColor = default, float duration = 0f)
    {
        Vector3[] v = _cacheUnitSphere;
        int len = v.Length / 3;

        for (int i = 0; i < horzDivisions; i++)
        {
            var rotAmnt = Quaternion.AngleAxis(i * (180 / horzDivisions), Vector3.forward);
            for (int j = 0; j < len; j++)
            {
                var start = rotAmnt * (radius * v[2 * len + j]);
                var end = rotAmnt * (radius * v[2 * len + (j + 1) % len]);

                start += pos;
                end += pos;
                Debug.DrawLine(start, end, horzColor, duration);
            }
        }

        for (int i = 0; i < vertDivisions; i++)
        {
            var rotAmnt = Quaternion.AngleAxis(i * (180 / vertDivisions), Vector3.up);
            for (int j = 0; j < len; j++)
            {
                var start = rotAmnt * (radius * v[1 * len + j]);
                var end = rotAmnt * (radius * v[1 * len + (j + 1) % len]);

                start += pos;
                end += pos;
                Debug.DrawLine(start, end, vertColor, duration);
            }
        }
    }
    private static Vector3[] _cacheUnitSphere = MakeUnitSphere(16);
    /// <summary>
    /// Makes a unit circle out of points. Three rings of points for each axis;<br/>
    /// each ring has <paramref name="len"/> points.
    /// </summary>
    private static Vector3[] MakeUnitSphere(int len)
    {
        Debug.Assert(len > 2);
        var v = new Vector3[len * 3];
        for (int i = 0; i < len; i++)
        {
            var f = i / (float)len;
            float cosNum = Mathf.Cos(f * Mathf.PI * 2.0f);
            float sinNum = Mathf.Sin(f * Mathf.PI * 2.0f);
            v[0 * len + i] = new Vector3(cosNum, sinNum, 0);
            v[1 * len + i] = new Vector3(0, cosNum, sinNum);
            v[2 * len + i] = new Vector3(sinNum, 0, cosNum);
        }
        return v;
    }

    /// <param name="xAxis">If <see langword="default"/> (zero alpha black), will be set to <see cref="Color.red"/>.</param>
    /// <param name="yAxis">If <see langword="default"/> (zero alpha black), will be set to <see cref="Color.green"/>.</param>
    /// <param name="zAxis">If <see langword="default"/> (zero alpha black), will be set to <see cref="Color.blue"/>.</param>
    public static void DrawAxes(Transform axesOwner, float length = 1f, float duration = 0f,
        Color xAxis = default, Color yAxis = default, Color zAxis = default)
    {
        if (xAxis == default) xAxis = Color.red;
        if (yAxis == default) yAxis = Color.green;
        if (zAxis == default) zAxis = Color.blue;

        Debug.DrawRay(axesOwner.position, axesOwner.right * length, xAxis, duration);
        Debug.DrawRay(axesOwner.position, axesOwner.up * length, yAxis, duration);
        Debug.DrawRay(axesOwner.position, axesOwner.forward * length, zAxis, duration);
    }

    /// <summary>
    /// Checks to see if this float is equal to <paramref name="target"/>, within a 
    /// given <paramref name="range"/>.
    /// </summary>
    public static bool EqualWithinRange(this float subject, float target, float range)
        => subject >= target - range && subject <= target + range;

    /// <inheritdoc cref="EqualWithinRange(float, float, float)"/>
    public static bool EqualWithinRange(this Vector3 subject, Vector3 target, float range)
        => (target - subject).sqrMagnitude <= range * range;

    /// <summary>
    /// Returns a Vector3 where XYZ = HSV, via <see cref="Color.RGBToHSV(Color, out float, out float, out float)"/>.
    /// </summary>
    public static Vector3 ToHSV(this Color c)
    {
        Vector3 result;
        Color.RGBToHSV(c, out result.x, out result.y, out result.z);
        return result;
    }

    /// <summary>
    /// Sets one channel (RGBA, 0123) of a color to <paramref name="value"/> and returns the result. 
    /// Optionally allows adding instead.
    /// </summary>
    /// <remarks>Will return the color unchanged if <paramref name="indexToAdjust"/> is invalid (i&lt;0, i&gt;3).</remarks>
    public static Color Adjust(this Color c, int indexToAdjust, float value, bool addValue = false)
    {
        if (indexToAdjust < 0 || indexToAdjust > 3) return c;

        c[indexToAdjust] = addValue ? c[indexToAdjust] + value : value;
        return c;
    }
    /// <remarks>
    /// <b>Note this is for <see cref="Color32"/>s; values should be between 0 and 255.</b><br/>
    /// Will return the color unchanged if <paramref name="indexToAdjust"/> is invalid (i&lt;0, i&gt;3).
    /// </remarks>
    /// <inheritdoc cref="Adjust(Color, int, float, bool)"/>
    public static Color32 Adjust(this Color32 c, int indexToAdjust, byte value, bool addValue = false)
    {
        if (indexToAdjust < 0 || indexToAdjust > 3) return c;

        c[indexToAdjust] = addValue ? (byte)(c[indexToAdjust] + value) : value;
        return c;
    }

    /// <summary>
    /// Sets one component (XYZ, 012) of a vector to <paramref name="value"/> and returns the result.
    /// Optionally allows adding instead.
    /// </summary>
    /// <remarks>Will return the vector unchanged if <paramref name="indexToAdjust"/> is invalid (i&lt;0, i&gt;2).</remarks>
    public static Vector3 Adjust(this Vector3 v, int indexToAdjust, float value, bool addValue = false)
    {
        if (indexToAdjust < 0 || indexToAdjust > 2) return v;

        v[indexToAdjust] = addValue ? v[indexToAdjust] + value : value;
        return v;
    }

    /// <summary>
    /// Returns a vector with the values at <paramref name="index1"/> and <paramref name="index2"/> swapped (XYZ, 012).
    /// </summary>
    /// <remarks>Will return the vector unchanged if either index is invalid (i&lt;0, i&gt;2).</remarks>
    public static Vector3 SwapAxes(this Vector3 v, int index1, int index2)
    {
        if (index1 < 0 || index1 > 2) return v;
        if (index2 < 0 || index2 > 2) return v;

        //witchcraft sourced from https://twitter.com/FreyaHolmer/status/1283000617510854656
        (v[index1], v[index2]) = (v[index2], v[index1]);
        return v;
    }

    public static Vector3 ClampComponents(Vector3 v,
        float xMin, float xMax,
        float yMin, float yMax,
        float zMin, float zMax)
    {
        v.x = Mathf.Clamp(v.x, xMin, xMax);
        v.y = Mathf.Clamp(v.y, yMin, yMax);
        v.z = Mathf.Clamp(v.z, zMin, zMax);
        return v;
    }
    public static Vector3 ClampComponents(Vector3 v, Vector3 minComponents, Vector3 maxComponents) =>
        ClampComponents(v,
            minComponents.x, maxComponents.x,
            minComponents.y, maxComponents.y,
            minComponents.z, maxComponents.z);
    public static Vector3 ClampComponents(Vector3 v, float min, float max) =>
        ClampComponents(v, min, max, min, max, min, max);

    /// <summary>
    /// Returns the closest value to <paramref name="v"/> that's outside the range (<paramref name="rangeMin"/>, 
    /// <paramref name="rangeMax"/>).<br/>
    /// Returns <paramref name="rangeMax"/> if equidistant to both edges.
    /// </summary>
    public static float ClampOutside(float v, float rangeMin, float rangeMax)
    {
        if (v < rangeMin || v > rangeMax)
            return v;

        return v - rangeMin < rangeMax - v
            ? rangeMin
            : rangeMax;
    }

    /// <summary>Of two or more values, returns the one with the largest absolute value.</summary>
    public static float MaxAbs(params float[] values)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Abs(values[i]);

        return Mathf.Max(values);
    }
    /// <inheritdoc cref="MaxAbs(float[])"/>
    public static float MaxAbs(params int[] values)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Abs(values[i]);

        return Mathf.Max(values);
    }
    /// <inheritdoc cref="MaxAbs(float[])"/>
    public static float MaxAbs(float a, float b) => Mathf.Abs(a) > Mathf.Abs(b) ? a : b;
    /// <inheritdoc cref="MaxAbs(float[])"/>
    public static float MaxAbs(int a, int b) => Mathf.Abs(a) > Mathf.Abs(b) ? a : b;

    /// <summary>Of two or more values, returns the one with the smallest absolute value.</summary>
    public static float MinAbs(params float[] values)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Abs(values[i]);

        return Mathf.Min(values);
    }
    /// <inheritdoc cref="MinAbs(float[])"/>
    public static float MinAbs(params int[] values)
    {
        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Abs(values[i]);

        return Mathf.Min(values);
    }
    /// <inheritdoc cref="MinAbs(float[])"/>
    public static float MinAbs(float a, float b) => Mathf.Abs(a) < Mathf.Abs(b) ? a : b;
    /// <inheritdoc cref="MinAbs(float[])"/>
    public static float MinAbs(int a, int b) => Mathf.Abs(a) < Mathf.Abs(b) ? a : b;

    public static float MaxComponent(this Vector3 v) => Mathf.Max(v.x, v.y, v.z);
    public static float MinComponent(this Vector3 v) => Mathf.Min(v.x, v.y, v.z);

    /// <summary>
    /// Divides two vectors component-wise.
    /// </summary>
    public static Vector3 InverseScale(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

    public static Vector3 inverseLocalScale(this Transform tform) => InverseScale(Vector3.one, tform.localScale);

    /// <summary>
    /// Scales this transform so that it's sized as if its parent had a scale of (1,1,1).
    /// </summary>
    /// <param name="parentLevel">The number of parents to go up by. 
    /// 0 = parent, 1 = grandparent (parent.parent), etc.</param>
    public static void NegateParentScale(this Transform tform, int parentLevel = 0)
    {
        Transform targetParent = tform.parent;
        for (int i = 0; i < parentLevel; i++)
        {
            if (!targetParent.parent)
                break;

            targetParent = targetParent.parent;
        }

        tform.localScale = InverseScale(tform.localScale, targetParent.localScale);
    }

    /// <summary>
    /// Lerps between <paramref name="from"/>-&gt;<paramref name="mid"/>-&gt;<paramref name="to"/>, based
    /// on <paramref name="t"/>.<br/>
    /// Optionally allows setting the mid point's "time" [0-1] to something other than 0.5.
    /// </summary>
    /// <param name="midTime">
    ///     <paramref name="mid"/>'s "position" along the 0-1 curve between 
    ///     <paramref name="from"/>-&gt;<paramref name="mid"/>-&gt;<paramref name="to"/>.<br/>
    ///     0.5 = equal distance to both end points.
    /// </param>
    public static float Lerp3Point(float from, float mid, float to, float t, float midTime = 0.5f)
    {
        if (t <= midTime)
            return Mathf.Lerp(from, mid, t * 2);
        else
            return Mathf.Lerp(mid, to, (t - 0.5f) * 2);
    }

    /// <summary>
    /// Calls <see cref="GameObject.SetActive(bool)"/> on this game object if it's not null or pending destroy.
    /// </summary>
    /// <returns>Was <see cref="GameObject.SetActive(bool)"/> called successfully?</returns>
    public static bool SafeSetActive(this GameObject obj, bool active)
    {
        if (obj)
        {
            obj.SetActive(active);
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="SafeSetActive(GameObject, bool)"/>
    public static bool SafeSetActive(Component objSource, bool active)
    {
        if (objSource) return objSource.gameObject.SafeSetActive(active);

        return false;
    }

    /// <summary>
    /// Takes a collection and, in the order of the collection, adds all distinct elements 
    /// of it to <paramref name="destination"/>.
    /// </summary>
    /// <param name="clearDest">Whether to <see cref="ICollection{T}.Clear"/> 
    /// <paramref name="destination"/> before adding to it.</param>
    public static void DistinctNonAlloc<T>(IEnumerable<T> source, IList<T> destination, bool clearDest = false)
    {
        if (clearDest)
            destination.Clear();

        foreach (var item in source)
        {
            if (!destination.Contains(item))
            {
                destination.Add(item);
            }
        }
    }

    /// <summary>
    /// Takes an ordered collection and, in the order of the collection, adds its elements to 
    /// <paramref name="destination"/>, sans any elements identical<br/>to the one immediately before.
    /// </summary>
    /// <param name="clearDest">Whether to <see cref="ICollection{T}.Clear"/> 
    /// <paramref name="destination"/> before adding to it.</param>
    public static void RemoveAdjacentDuplicatesNonAlloc<T>(
        IList<T> source, IList<T> destination, bool clearDest = true)
    {
        if (clearDest)
            destination.Clear();

        for (int i = 0; i < source.Count; i++)
        {
            if (i < 1 || !EquCompEquals(source[i - 1], source[i]))
            {
                destination.Add(source[i]);
            }
        }
    }

    /// <summary>
    /// A shorthand function for <see cref="EqualityComparer{T}.Default.Equals(T, T)"/>.
    /// </summary>
    public static bool EquCompEquals<T>(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);

    public static bool CompareTagInParentsAndChildren(Transform subject, string tag,
        int levelsUp = int.MaxValue, int levelsDown = int.MaxValue,
        bool checkSelf = true, bool parentsFirst = true)
    {
        if (parentsFirst)
        {
            if (CompareTagInParents(subject, tag, levelsUp, checkSelf)) return true;
            if (CompareTagInChildren(subject, tag, levelsDown, false)) return true;
        }
        else
        {
            if (CompareTagInChildren(subject, tag, levelsDown, checkSelf)) return true;
            if (CompareTagInParents(subject, tag, levelsUp, false)) return true;
        }

        return false;
    }

    public static bool CompareTagInParents(Transform subject, string tag, int levels = int.MaxValue, bool checkSelf = true)
    {
        if (checkSelf && subject.CompareTag(tag)) return true;

        //Get the parent of subject. Then, so long as there are parents (and we haven't gone past the number of
        //levels specified), CompareTag on those parents.
        var next = subject.parent;
        while (next && levels > 0)
        {
            if (next.CompareTag(tag)) return true;

            next = next.parent;
            levels--;
        }

        return false;
    }
    public static bool CompareTagInParents(Component subject, string tag, int levels = int.MaxValue, bool checkSelf = true)
        => CompareTagInParents(subject.transform, tag, levels, checkSelf);

    public static bool CompareTagInChildren(Transform subject, string tag, int levels = int.MaxValue, bool checkSelf = true)
    {
        if (checkSelf && subject.CompareTag(tag)) return true;
        if (levels < 1) return false;

        //Compare the tags on each child. If this is the last level (level <= 1), the recursion won't continue inside these checks.
        foreach (Transform child in subject)
            if (CompareTagInChildren(child, tag, levels - 1, true)) return true;

        return false;
    }
    public static bool CompareTagInChildren(Component subject, string tag, int levels = int.MaxValue, bool checkSelf = true)
        => CompareTagInChildren(subject.transform, tag, levels, checkSelf);
}