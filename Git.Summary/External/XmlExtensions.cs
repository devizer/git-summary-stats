using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Universe;

public static class XmlExtensions
{
    public static IEnumerable<XmlElement> GetSubElements(this XmlElement xmlElement, string elementName)
    {
        var ret = xmlElement.ChildNodes.OfType<XmlElement>().Where(x => x.Name == elementName);
        return ret;
    }
    public static string GetStringAttribute(this XmlElement xmlElement, string attributeName)
    {
        var attr = xmlElement.Attributes.OfType<XmlAttribute>().Where(x => x.Name == attributeName).FirstOrDefault();
        return attr?.Value;
    }
    public static decimal? GetDecimalAttribute(this XmlElement xmlElement, string attributeName)
    {
        var stringValue = xmlElement.GetStringAttribute(attributeName);
        if (string.IsNullOrEmpty(stringValue)) return null;
        if (decimal.TryParse(stringValue, out var dec))
            return dec;

        throw new ArgumentException($"Attribute '{attributeName}' value '{stringValue}' is not a decimal");
    }
    public static decimal? GetDecimalPtAttribute(this XmlElement xmlElement, string attributeName)
    {
        var stringValue = xmlElement.GetStringAttribute(attributeName);
        if (string.IsNullOrEmpty(stringValue)) return null;
        var stringNormalized = stringValue;
        if (stringNormalized.EndsWith("pt"))
        {
            stringNormalized = stringNormalized.Length > 2 ? stringNormalized.Substring(0, stringNormalized.Length - 2) : "";
        }
        if (decimal.TryParse(stringNormalized, out var dec))
            return dec;

        throw new ArgumentException($"Attribute '{attributeName}' value '{stringValue}' is not a decimal");
    }



}