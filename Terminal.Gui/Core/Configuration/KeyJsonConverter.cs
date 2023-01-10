﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Terminal.Gui.Core {
	/// <summary>
	/// 
	/// </summary>
	public class KeyJsonConverter : JsonConverter<Key> {
		/// <summary>
		/// 
		/// </summary>
		/// <param name="reader"></param>
		/// <param name="typeToConvert"></param>
		/// <param name="options"></param>
		/// <returns></returns>
		/// <exception cref="JsonException"></exception>
		public override Key Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.StartObject) {
				Key key = Key.Unknown;;
				Dictionary<string, Key> modifierDict = new Dictionary<string, Key> ();
				modifierDict.Add ("Shift", Key.ShiftMask);
				modifierDict.Add ("Ctrl", Key.CtrlMask);
				modifierDict.Add ("Alt", Key.AltMask);

				List<Key> modifiers = new List<Key> ();

				while (reader.Read ()) {
					if (reader.TokenType == JsonTokenType.EndObject) {
						break;
					}

					if (reader.TokenType == JsonTokenType.PropertyName) {
						string propertyName = reader.GetString ();
						reader.Read ();

						switch (propertyName) {
						case "Key":
							if (reader.TokenType == JsonTokenType.String) {
								Enum.TryParse(reader.GetString(), false, out key);
								if (key == Key.Unknown) {
									throw new JsonException ("If Key is a string, it must match a constant in the Key enum.");
								}
								break;
							}
							if (reader.TokenType == JsonTokenType.Number) {
								key = (Key)reader.GetInt32 ();
								break;
							}

							throw new JsonException ("Key is invalid.");

						case "Modifiers":
							if (reader.TokenType == JsonTokenType.StartArray) {
								while (reader.Read ()) {
									if (reader.TokenType == JsonTokenType.EndArray) {
										break;
									}
									var mod = reader.GetString ();
									modifiers.Add (modifierDict [mod]);
								}
							}
							break;
						}
					}
				}

				foreach (var modifier in modifiers) {
					key |= modifier;
				}

				return key;
			}

			throw new JsonException ();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="value"></param>
		/// <param name="options"></param>
		public override void Write (Utf8JsonWriter writer, Key value, JsonSerializerOptions options)
		{
			writer.WriteStartObject ();

			var keyName = (value & ~Key.CtrlMask & ~Key.ShiftMask & ~Key.AltMask).ToString();
			if (keyName != null) {
				writer.WriteString ("Key", keyName);
			} else {
				writer.WriteNumber ("Key", (uint)(value & ~Key.CtrlMask & ~Key.ShiftMask & ~Key.AltMask));
			}

			Dictionary<string, Key> modifierDict = new Dictionary<string, Key>
			{
				{ "Shift", Key.ShiftMask },
				{ "Ctrl", Key.CtrlMask },
				{ "Alt", Key.AltMask }
			    };

			List<string> modifiers = new List<string> ();
			foreach (var pair in modifierDict) {
				if ((value & pair.Value) == pair.Value) {
					modifiers.Add (pair.Key);
				}
			}

			if (modifiers.Count > 0) {
				writer.WritePropertyName ("Modifiers");
				writer.WriteStartArray ();
				foreach (var modifier in modifiers) {
					writer.WriteStringValue (modifier);
				}
				writer.WriteEndArray ();
			}

			writer.WriteEndObject ();
		}

	}
}
