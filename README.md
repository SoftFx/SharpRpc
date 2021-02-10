**SharpRpc is fast and efficient RPC for .NET Framework and .NET Core.**

# Freatures

  * High efficiency (especialy while dealing with lot of small messages)
  * Low latency
  * Direct and backward calls (callbacks) in one contract
  * Support of grpc-like streams
  * Configureable back-pressure for streams and ordinary calls
  * Optimization for multicasting messages and stream items through pre-serialized messages
  * Support of asynchronous calls
  * Extensibility (transports, serilizers, authificators and etc are abstract and can be replaced by custom implementation)

# Limitations

  * SharpRpc supports only transport-level authificcation.
  * SharpRpc is not a multi-platform protocol (.Net only).
  



