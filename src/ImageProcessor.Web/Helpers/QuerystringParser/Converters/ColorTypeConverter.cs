﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorTypeConverter.cs" company="James Jackson-South">
//   Copyright (c) James Jackson-South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The extended color type converter allows conversion of system and web colors.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Helpers
{
    using System;
    using System.Collections;
    using System.Drawing;
    using System.Globalization;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The color type converter allows conversion of system and web colors.
    /// </summary>
    public class ColorTypeConverter : QueryParamConverter
    {
        /// <summary>
        /// The web color regex.
        /// </summary>
        private static readonly Regex HexColorRegex = new Regex("([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        /// <summary>
        /// The number color regex.
        /// </summary>
        private static readonly Regex NumberRegex = new Regex(@"\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        /// <summary>
        /// The system color table map.
        /// </summary>
        private static readonly Lazy<Hashtable> SystemColorTable = new Lazy<Hashtable>(InitializeHtmlSystemColorTable);

        /// <summary>
        /// The color constants table map.
        /// </summary>
        private static readonly Lazy<Hashtable> ColorConstantsTable = new Lazy<Hashtable>(InitializeColorConstantsTable);

        /// <summary>
        /// Gets the html system color table.
        /// </summary>
        private static Hashtable SystemColors => SystemColorTable.Value;

        /// <summary>
        /// Gets the color constants table.
        /// </summary>
        private static Hashtable ColorConstants => ColorConstantsTable.Value;

        /// <summary>
        /// Returns whether this converter can convert an object of the given type to the type of this converter, 
        /// using the specified context.
        /// </summary>
        /// <returns>
        /// true if this converter can perform the conversion; otherwise, false.
        /// </returns>
        /// <param name="sourceType">
        /// A <see cref="T:System.Type"/> that represents the type you want to convert from. 
        /// </param>
        public override bool CanConvertFrom(Type sourceType)
        {
            if (sourceType == typeof(string))
            {
                return true;
            }

            return base.CanConvertFrom(sourceType);
        }

        /// <summary>
        /// Converts the given object to the type of this converter, using the specified culture 
        /// information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="culture">
        /// The <see cref="T:System.Globalization.CultureInfo"/> to use as the current culture. 
        /// </param>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <param name="propertyType">The property type that the converter will convert to.</param>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed.</exception>
        public override object ConvertFrom(CultureInfo culture, object value, Type propertyType)
        {
            if (value == null)
            {
                return Color.Empty;
            }

            if (value is string s)
            {
                string colorText = s.Trim();
                Color c = Color.Empty;

                // Empty color 
                if (string.IsNullOrEmpty(colorText))
                {
                    return c;
                }

                // Special case. HTML requires LightGrey, but System.Drawing.KnownColor has LightGray 
                if (colorText.Equals("LightGrey", StringComparison.OrdinalIgnoreCase))
                {
                    return Color.LightGray;
                }

                // Handle a,r,g,b
                // Converter can be called externally.
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (culture == null)
                {
                    culture = CultureInfo.CurrentCulture;
                }

                char separator = culture.TextInfo.ListSeparator[0];

                if (colorText.Contains(separator.ToString()))
                {
                    string[] components = colorText.Split(separator);

                    bool convert = true;
                    foreach (string component in components)
                    {
                        if (!NumberRegex.IsMatch(component))
                        {
                            convert = false;
                        }
                    }

                    if (convert)
                    {
                        if (components.Length == 4)
                        {
                            return Color.FromArgb(
                                Convert.ToInt32(components[3]),
                                Convert.ToInt32(components[0]),
                                Convert.ToInt32(components[1]),
                                Convert.ToInt32(components[2]));
                        }

                        return Color.FromArgb(
                            Convert.ToInt32(components[0]),
                            Convert.ToInt32(components[1]),
                            Convert.ToInt32(components[2]));
                    }
                }

                // Hex based color values.
                char hash = colorText[0];
                if (hash == '#' || HexColorRegex.IsMatch(colorText))
                {
                    if (hash != '#')
                    {
                        colorText = "#" + colorText;
                    }

                    switch (colorText.Length)
                    {
                        case 4:

                            // 4 charcters eg: #0f0
                            string r = char.ToString(colorText[1]);
                            string g = char.ToString(colorText[2]);
                            string b = char.ToString(colorText[3]);

                            return Color.FromArgb(
                                Convert.ToInt32(r + r, 16),
                                Convert.ToInt32(g + g, 16),
                                Convert.ToInt32(b + b, 16));

                        case 7:

                            // 7 charcters eg: #00ff00
                            return Color.FromArgb(
                                Convert.ToInt32(colorText.Substring(1, 2), 16),
                                Convert.ToInt32(colorText.Substring(3, 2), 16),
                                Convert.ToInt32(colorText.Substring(5, 2), 16));

                        case 9:

                            // 9 characters, starting with alpha eg: #ff00ff00
                            return Color.FromArgb(
                                Convert.ToInt32(colorText.Substring(1, 2), 16),
                                Convert.ToInt32(colorText.Substring(3, 2), 16),
                                Convert.ToInt32(colorText.Substring(5, 2), 16),
                                Convert.ToInt32(colorText.Substring(7, 2), 16));
                    }
                }

                // System and named color constants.
                object namedColor = GetNamedColor(colorText);
                if (namedColor != null)
                {
                    return (Color)namedColor;
                }
            }

            return base.ConvertFrom(culture, value, propertyType);
        }

        /// <summary>
        /// Converts the given value object to the specified type, using the specified context and culture 
        /// information.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Object"/> that represents the converted value.
        /// </returns>
        /// <param name="culture">
        /// A <see cref="T:System.Globalization.CultureInfo"/>. If null is passed, the current culture is assumed. 
        /// </param>
        /// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
        /// <param name="destinationType">
        /// The <see cref="T:System.Type"/> to convert the <paramref name="value"/> parameter to. 
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// The <paramref name="destinationType"/> parameter is null. 
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. 
        /// </exception>
        public override object ConvertTo(CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null)
            {
                throw new ArgumentNullException(nameof(destinationType));
            }

            if (destinationType == typeof(string) && value != null)
            {
                var color = (Color)value;

                if (color == Color.Empty)
                {
                    return string.Empty;
                }

                if (color.IsKnownColor)
                {
                    return color.Name;
                }

                if (color.IsNamedColor)
                {
                    return "'" + color.Name + "'";
                }

                // In the Web scenario, colors should be formatted in #RRGGBB notation 
                var sb = new StringBuilder("#", 7);
                sb.Append(color.R.ToString("X2", CultureInfo.InvariantCulture));
                sb.Append(color.G.ToString("X2", CultureInfo.InvariantCulture));
                sb.Append(color.B.ToString("X2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }

            return base.ConvertTo(culture, value, destinationType);
        }

        /// <summary>
        /// Gets the named color from the given name
        /// </summary>
        /// <param name="name">The name of the color</param>
        /// <returns><see cref="object"/></returns>
        internal static object GetNamedColor(string name)
        {
            // First, check to see if this is a standard name.
            object color = ColorConstants[name];
            return color ?? SystemColors[name];
        }

        /// <summary>
        /// Initializes color table mapping system colors to known colors.
        /// </summary>
        /// <returns>The <see cref="Hashtable"/></returns>
        private static Hashtable InitializeHtmlSystemColorTable()
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var hashTable = new Hashtable(StringComparer.OrdinalIgnoreCase)
            {
                ["activeborder"] = Color.FromKnownColor(KnownColor.ActiveBorder),
                ["activecaption"] = Color.FromKnownColor(KnownColor.ActiveCaption),
                ["appworkspace"] = Color.FromKnownColor(KnownColor.AppWorkspace),
                ["background"] = Color.FromKnownColor(KnownColor.Desktop),
                ["buttonface"] = Color.FromKnownColor(KnownColor.Control),
                ["buttonhighlight"] = Color.FromKnownColor(KnownColor.ControlLightLight),
                ["buttonshadow"] = Color.FromKnownColor(KnownColor.ControlDark),
                ["buttontext"] = Color.FromKnownColor(KnownColor.ControlText),
                ["captiontext"] = Color.FromKnownColor(KnownColor.ActiveCaptionText),
                ["graytext"] = Color.FromKnownColor(KnownColor.GrayText),
                ["highlight"] = Color.FromKnownColor(KnownColor.Highlight),
                ["highlighttext"] = Color.FromKnownColor(KnownColor.HighlightText),
                ["inactiveborder"] = Color.FromKnownColor(KnownColor.InactiveBorder),
                ["inactivecaption"] = Color.FromKnownColor(KnownColor.InactiveCaption),
                ["inactivecaptiontext"] = Color.FromKnownColor(KnownColor.InactiveCaptionText),
                ["infobackground"] = Color.FromKnownColor(KnownColor.Info),
                ["infotext"] = Color.FromKnownColor(KnownColor.InfoText),
                ["menu"] = Color.FromKnownColor(KnownColor.Menu),
                ["menutext"] = Color.FromKnownColor(KnownColor.MenuText),
                ["scrollbar"] = Color.FromKnownColor(KnownColor.ScrollBar),
                ["threeddarkshadow"] = Color.FromKnownColor(KnownColor.ControlDarkDark),
                ["threedface"] = Color.FromKnownColor(KnownColor.Control),
                ["threedhighlight"] = Color.FromKnownColor(KnownColor.ControlLight),
                ["threedlightshadow"] = Color.FromKnownColor(KnownColor.ControlLightLight),
                ["window"] = Color.FromKnownColor(KnownColor.Window),
                ["windowframe"] = Color.FromKnownColor(KnownColor.WindowFrame),
                ["windowtext"] = Color.FromKnownColor(KnownColor.WindowText)
            };

            return hashTable;
        }

        /// <summary>
        /// Initializes color table mapping color constants.
        /// </summary>
        /// <returns>The <see cref="Hashtable"/></returns>
        private static Hashtable InitializeColorConstantsTable()
        {
            var hashTable = new Hashtable(StringComparer.OrdinalIgnoreCase);

            const MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.Static;
            PropertyInfo[] props = typeof(Color).GetProperties();

            foreach (PropertyInfo prop in props)
            {
                if (prop.PropertyType == typeof(Color))
                {
                    MethodInfo method = prop.GetGetMethod();
                    if (method != null && (method.Attributes & attrs) == attrs)
                    {
                        hashTable[prop.Name] = prop.GetValue(null, null);
                    }
                }
            }

            return hashTable;
        }
    }
}
