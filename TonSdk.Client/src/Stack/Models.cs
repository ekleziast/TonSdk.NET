using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using TonSdk.Core;
using TonSdk.Core.Boc;

namespace TonSdk.Client.Stack
{
    [Serializable]
    public class Stack
    {
        [JsonPropertyName("stack_items")] public List<IStackItem> StackItems;

        public Stack(IStackItem[] stackItems)
        {
            StackItems = stackItems.ToList();
        }
    }

    public interface IStackItem
    {
    }

    [Serializable]
    public struct VmStackNull : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackNull";
    }

    [Serializable]
    public struct VmStackTinyInt : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackTinyInt";
        [JsonPropertyName("value")] public long Value { get; set; }

        public VmStackTinyInt(object value)
        {
            if (long.TryParse(value.ToString(), out long result))
                Value = result;
            else throw new ArgumentException("Wrong argument type.");
        }
    }

    [Serializable]
    public struct VmStackInt : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackInt";
        [JsonPropertyName("value")] public BigInteger Value { get; set; }
        public VmStackInt(object value)
        {
            if (value is Coins coins)
            {
                Value = coins.ToBigInt();
                return;
            }
            
            if (BigInteger.TryParse(value.ToString(), out var result))
                Value = result;
            else throw new ArgumentException("Wrong argument type.");
        }
    }

    [Serializable]
    public struct VmStackCell : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackCell";
        [JsonPropertyName("cell")] public Cell Value { get; set; }
        
        public VmStackCell(Cell value)
        {
            Value = value;
        }
    }

    [Serializable]
    public struct VmStackSlice : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackSlice";
        [JsonPropertyName("slice")] public CellSlice Value { get; set; }
        
        public VmStackSlice(Address value)
        {
            Value = new CellBuilder().StoreAddress(value).Build().Parse();
        }
        
        public VmStackSlice(CellSlice value)
        {
            Value = value;
        }
    }

    [Serializable]
    public struct VmStackBuilder : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackBuilder";
        [JsonPropertyName("builder")] public CellBuilder Value { get; set; }
        
        public VmStackBuilder(CellBuilder value)
        {
            Value = value;
        }
    }

    [Serializable]
    public struct VmStackTuple : IStackItem
    {
        [JsonPropertyName("key")] public const string Key = "VmStackTuple";
        [JsonPropertyName("tuple")] public IStackItem[] Value { get; set; }
    }

    [Serializable]
    internal struct StackJsonItem
    {
        [JsonPropertyName("type")] public string Type;
        [JsonPropertyName("value")] public string Value;
    }
}