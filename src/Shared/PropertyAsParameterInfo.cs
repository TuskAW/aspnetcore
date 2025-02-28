// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Http;

internal sealed class PropertyAsParameterInfo : ParameterInfo
{
    private readonly PropertyInfo _underlyingProperty;
    private readonly ParameterInfo? _constructionParameterInfo;

    private readonly NullabilityInfoContext _nullabilityContext;
    private NullabilityInfo? _nullabilityInfo;

    public PropertyAsParameterInfo(PropertyInfo propertyInfo, NullabilityInfoContext? nullabilityContext = null)
    {
        Debug.Assert(null != propertyInfo);

        AttrsImpl = (ParameterAttributes)propertyInfo.Attributes;
        NameImpl = propertyInfo.Name;
        MemberImpl = propertyInfo;
        ClassImpl = propertyInfo.PropertyType;

        // It is not a real parameter in the delegate, so,
        // not defining a real position.
        PositionImpl = -1;

        _nullabilityContext = nullabilityContext ?? new NullabilityInfoContext();
        _underlyingProperty = propertyInfo;
    }

    public PropertyAsParameterInfo(PropertyInfo property, ParameterInfo parameterInfo, NullabilityInfoContext? nullabilityContext = null)
        : this(property, nullabilityContext)
    {
        _constructionParameterInfo = parameterInfo;
    }

    public override bool HasDefaultValue
        => _constructionParameterInfo is not null && _constructionParameterInfo.HasDefaultValue;
    public override object? DefaultValue
        => _constructionParameterInfo is not null ? _constructionParameterInfo.DefaultValue : null;
    public override int MetadataToken => _underlyingProperty.MetadataToken;
    public override object? RawDefaultValue
        => _constructionParameterInfo is not null ? _constructionParameterInfo.RawDefaultValue : null;

    /// <summary>
    /// Unwraps all parameters that contains <see cref="AsParametersAttribute"/> and
    /// creates a flat list merging the current parameters, not including the
    /// parametres that contain a <see cref="AsParametersAttribute"/>, and all additional
    /// parameters detected.
    /// </summary>
    /// <param name="parameters">List of parameters to be flattened.</param>
    /// <param name="cache">An instance of the method cache class.</param>
    /// <returns>Flat list of parameters.</returns>
    [UnconditionalSuppressMessage("Trimmer", "IL2075", Justification = "PropertyAsParameterInfo.Flatten requires unreferenced code.")]
    public static ReadOnlySpan<ParameterInfo> Flatten(ParameterInfo[] parameters, ParameterBindingMethodCache cache)
    {
        ArgumentNullException.ThrowIfNull(nameof(parameters));
        ArgumentNullException.ThrowIfNull(nameof(cache));

        if (parameters.Length == 0)
        {
            return Array.Empty<ParameterInfo>();
        }

        List<ParameterInfo>? flattenedParameters = null;
        NullabilityInfoContext? nullabilityContext = null;

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].CustomAttributes.Any(a => a.AttributeType == typeof(AsParametersAttribute)))
            {
                // Initialize the list with all parameter already processed
                // to keep the same parameter ordering
                flattenedParameters ??= new(parameters[0..i]);
                nullabilityContext ??= new();

                var (constructor, constructorParameters) = cache.FindConstructor(parameters[i].ParameterType);
                if (constructor is not null && constructorParameters is { Length: > 0 })
                {
                    foreach (var constructorParameter in constructorParameters)
                    {
                        flattenedParameters.Add(
                            new PropertyAsParameterInfo(
                                constructorParameter.PropertyInfo,
                                constructorParameter.ParameterInfo,
                                nullabilityContext));
                    }
                }
                else
                {
                    var properties = parameters[i].ParameterType.GetProperties();

                    foreach (var property in properties)
                    {
                        if (property.CanWrite)
                        {
                            flattenedParameters.Add(new PropertyAsParameterInfo(property, nullabilityContext));
                        }
                    }
                }
            }
            else if (flattenedParameters is not null)
            {
                flattenedParameters.Add(parameters[i]);
            }
        }

        return flattenedParameters is not null ? CollectionsMarshal.AsSpan(flattenedParameters) : parameters.AsSpan();
    }

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        var attributes = _constructionParameterInfo?.GetCustomAttributes(attributeType, inherit);

        if (attributes == null || attributes is { Length: 0 })
        {
            attributes = _underlyingProperty.GetCustomAttributes(attributeType, inherit);
        }

        return attributes;
    }

    public override object[] GetCustomAttributes(bool inherit)
    {
        var constructorAttributes = _constructionParameterInfo?.GetCustomAttributes(inherit);

        if (constructorAttributes == null || constructorAttributes is { Length: 0 })
        {
            return _underlyingProperty.GetCustomAttributes(inherit);
        }

        var propertyAttributes = _underlyingProperty.GetCustomAttributes(inherit);

        // Since the constructors attributes should take priority we will add them first,
        // as we usually call it as First() or FirstOrDefault() in the argument creation
        var mergedAttributes = new object[constructorAttributes.Length + propertyAttributes.Length];
        Array.Copy(constructorAttributes, mergedAttributes, constructorAttributes.Length);
        Array.Copy(propertyAttributes, 0, mergedAttributes, constructorAttributes.Length, propertyAttributes.Length);

        return mergedAttributes;
    }

    public override IList<CustomAttributeData> GetCustomAttributesData()
    {
        var attributes = new List<CustomAttributeData>(
            _constructionParameterInfo?.GetCustomAttributesData() ?? Array.Empty<CustomAttributeData>());
        attributes.AddRange(_underlyingProperty.GetCustomAttributesData());

        return attributes.AsReadOnly();
    }

    public override Type[] GetOptionalCustomModifiers()
        => _underlyingProperty.GetOptionalCustomModifiers();

    public override Type[] GetRequiredCustomModifiers()
        => _underlyingProperty.GetRequiredCustomModifiers();

    public override bool IsDefined(Type attributeType, bool inherit)
    {
        return (_constructionParameterInfo is not null && _constructionParameterInfo.IsDefined(attributeType, inherit)) ||
            _underlyingProperty.IsDefined(attributeType, inherit);
    }

    public new bool IsOptional => HasDefaultValue || NullabilityInfo.ReadState != NullabilityState.NotNull;

    public NullabilityInfo NullabilityInfo
        => _nullabilityInfo ??= _constructionParameterInfo is not null ?
        _nullabilityContext.Create(_constructionParameterInfo) :
        _nullabilityContext.Create(_underlyingProperty);
}
