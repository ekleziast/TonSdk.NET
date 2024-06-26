﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TonSdk.Client.Stack;
using TonSdk.Core;
using TonSdk.Core.Block;
using TonSdk.Core.Boc;
using static TonSdk.Client.Transformers;

namespace TonSdk.Client
{
    public class HttpApiV3 : IDisposable
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        private readonly HttpClient _httpClient;
        
        public HttpApiV3(HttpParameters httpApiParameters)
        {
            if (string.IsNullOrEmpty(httpApiParameters.Endpoint))
            {
                throw new ArgumentNullException("Endpoint field in Http options cannot be null.");
            }

            _httpClient = new HttpClient();

            _httpClient.Timeout = TimeSpan.FromMilliseconds(Convert.ToDouble(httpApiParameters.Timeout ?? 30000));
            
            _httpClient.DefaultRequestHeaders.Accept.Clear();
           
            
            if (!string.IsNullOrEmpty(httpApiParameters.ApiKey))
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", httpApiParameters.ApiKey);

            _httpClient.BaseAddress = new Uri(httpApiParameters.Endpoint);
        }
        
        internal async Task<AddressInformationResult> GetAddressInformation(Address address)
        {
            string result = await new TonRequestV3(new RequestParametersV3("account", new Dictionary<string, object>()
                {
                    {
                        "address", address.ToString()
                    }
                }), _httpClient).CallGet();

            var addressInformationResult = new AddressInformationResult(JsonSerializer.Deserialize<OutV3AddressInformationResult>(result, _jsonSerializerOptions));
            return addressInformationResult;
        }
        
        internal async Task<WalletInformationResult> GetWalletInformation(Address address)
        {
            string result = await new TonRequestV3(new RequestParametersV3("wallet", new Dictionary<string, object>()
            {
                {
                    "address", address.ToString()
                }
            }), _httpClient).CallGet();
            return result == "conflict" 
                ? new WalletInformationResult(await GetAddressInformation(address)) 
                : new WalletInformationResult(JsonSerializer.Deserialize<OutV3WalletInformationResult>(result, _jsonSerializerOptions));
        }
        
        internal async Task<MasterchainInformationResult> GetMasterchainInfo()
        {
            string result = await new TonRequestV3(new RequestParametersV3("masterchainInfo", new Dictionary<string, object>()), _httpClient).CallGet();
            return new MasterchainInformationResult(JsonSerializer.Deserialize<OutV3MasterchainInformationResult>(result, _jsonSerializerOptions));
        }
        
        internal async Task<BlockIdExtended> LookUpBlock(int workchain, long shard, long? seqno = null, ulong? lt = null, ulong? unixTime = null)
        {
            var req = new Dictionary<string, object>()
            {
                {
                    "workchain", workchain.ToString()
                },
                {
                    "shard", shard.ToString()
                },
                {
                    "offset", "0"
                },
                {
                    "limit", "1"
                },
                {
                    "sort", "asc"
                }
            };
            if (seqno != null)
                req.Add("seqno", seqno.Value.ToString());
            if (lt != null)
                req.Add("start_lt", lt.Value.ToString());
            if (unixTime != null)
                req.Add("start_utime", unixTime.Value.ToString());
            
            var result = await new TonRequestV3(new RequestParametersV3("blocks", req), _httpClient).CallGet();
            var blocks = JsonSerializer.Deserialize<RootV3LookUpBlock>(result, _jsonSerializerOptions).Blocks;
            
            return blocks.Length != 0 ? blocks[0] : null;
        }
        
        internal async Task<ShardsInformationResult> Shards(long seqno)
        {
            string result = await new TonRequestV3(new RequestParametersV3("masterchainBlockShardState", new Dictionary<string, object>()
            {
                {
                    "seqno", seqno.ToString()
                }
            }), _httpClient).CallGet();
            return new ShardsInformationResult(JsonSerializer.Deserialize<OutV3ShardsInformationResult>(result, _jsonSerializerOptions));
        }
        
        internal async Task<TransactionsInformationResult[]> GetTransactions(Address address, uint limit = 10,
            ulong? lt = null, string hash = null, ulong? to_lt = null, bool? archival = null)
        {
            var dict = new Dictionary<string, object>()
            {
                {
                    "account", address.ToString()
                },
                {
                    "offset", "0"
                },
                {
                    "limit", limit.ToString()
                },
                {
                    "sort", "desc"
                }
            };

            if (lt != null) 
                dict.Add("start_lt", lt.ToString());
            if (to_lt != null) 
                dict.Add("end_lt", to_lt.ToString());

            string result = await new TonRequestV3(new RequestParametersV3("transactions", dict), _httpClient).CallGet();
            
            var data = JsonSerializer.Deserialize<RootTransactions>(result, _jsonSerializerOptions).Transactions;
            return data.Select(t => new TransactionsInformationResult(t)).ToArray();
        }
        
        internal async Task<BlockTransactionsResult> GetBlockTransactions(
            int workchain,
            long shard,
            long seqno,
            string rootHash = null,
            string fileHash = null,
            ulong? afterLt = null,
            string afterHash = null,
            uint? count = null)
        {
            try
            {
                var dict = new Dictionary<string, object>()
                {
                    {
                        "workchain", workchain.ToString()
                    },
                    {
                        "shard", shard.ToString("X")
                    },
                    {
                        "seqno", seqno.ToString()
                    },
                    {
                        "offset", "0"
                    },
                    {
                        "limit", count != null ? count.ToString() : "10"
                    },
                    {
                        "sort", "desc"
                    }
                };

                if (afterLt != null) 
                    dict.Add("start_lt", afterLt.ToString());

                string result = await new TonRequestV3(new RequestParametersV3("transactions", dict), _httpClient).CallGet();
            
                var data = JsonSerializer.Deserialize<RootBlockTransactions>(result, _jsonSerializerOptions).Transactions;
                var transactions = data.Select(item => new ShortTransactionsResult() { Account = item.Account, Hash = item.Hash, Lt = item.Lt, Mode = item.Description.ComputePh.Mode }).ToList();
                return new BlockTransactionsResult()
                {
                    Id = new BlockIdExtended()
                    {
                        Seqno = seqno,
                        Workchain = workchain,
                        Shard = shard
                    },
                    ReqCount = count != null ? (int)count : 10,
                    Incomplete = false,
                    Transactions = transactions.ToArray()
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
        
        internal async Task<SendBocResult> SendBoc(Cell boc)
        {
            string result = await new TonRequestV3(new RequestParametersV3("message", new Dictionary<string, object>()
            {
                {
                    "boc", boc.ToString("base64")
                }
            }), _httpClient).CallPost();
            var resultRoot = JsonSerializer.Deserialize<RootSendBoc>(result, _jsonSerializerOptions);
            return new SendBocResult()
                {
                    Hash = resultRoot.MessageHash,
                    Type = "1"
                };
        }
        
        internal async Task<RunGetMethodResult> RunGetMethod(Address address, string method, StackJsonItem[] items)
        {
            string result = await new TonRequestV3(new RequestParametersV3("runGetMethod", new Dictionary<string, object>()
            {
                {
                    "address", address.ToString()
                },
                {
                    "method", method
                },
                {
                    "stack", items
                }
            }), _httpClient).CallPost();
            return new RunGetMethodResult(JsonSerializer.Deserialize<OutV3RunGetMethod>(result, _jsonSerializerOptions));
        }
        
        internal async Task<EstimateFeeResultExtended> EstimateFee(MessageX message, bool ignoreChksig = true)
        {
            var dataMsg = message.Data;

            Address address = (message.Data.Info.Data is IntMsgInfoOptions info) ? info.Dest :
                (message.Data.Info.Data is ExtInMsgInfoOptions info2) ? info2.Dest : null;

            Cell body = dataMsg.Body;
            Cell init_code = dataMsg.StateInit?.Data.Code;
            Cell init_data = dataMsg.StateInit?.Data.Data;

            string result = await new TonRequestV3(new RequestParametersV3("estimateFee", new Dictionary<string, object>()
            {
                {
                    "address", address.ToString()
                },
                {
                    "body", body?.ToString("base64") ?? string.Empty
                },
                {
                    "init_code", init_code?.ToString("base64") ?? string.Empty
                },
                {
                    "init_data", init_data?.ToString("base64") ?? string.Empty
                },
                {
                    "ignore_chksig", ignoreChksig
                }
            }), _httpClient).CallPost();
            
            return JsonSerializer.Deserialize<EstimateFeeResultExtended>(result, _jsonSerializerOptions);
        }

        public async Task<string> CustomGetMethodCall(string request, List<string[]> body)
        {
            return await new TonRequestV3(new RequestParametersV3(request, new Dictionary<string, object>()), _httpClient).CallGetList(body);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}