﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Spear.Core.Message;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Spear.Core.Micro.Implementation
{
    /// <summary> 默认服务客户端 </summary>
    public class MicroClient : IMicroClient, IDisposable
    {
        private readonly IMessageSender _sender;
        private readonly IMessageListener _listener;
        private readonly IMicroExecutor _executor;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<MicroMessage>> _resultDictionary;

        public MicroClient(ILogger logger, IMessageSender sender, IMessageListener listener, IMicroExecutor executor)
        {
            _sender = sender;
            _listener = listener;
            _executor = executor;
            _logger = logger;
            _resultDictionary = new ConcurrentDictionary<string, TaskCompletionSource<MicroMessage>>();
            listener.Received += ListenerOnReceived;
        }

        private async Task ListenerOnReceived(IMessageSender sender, MicroMessage message)
        {
            if (!_resultDictionary.TryGetValue(message.Id, out var task))
                return;

            if (message.IsResult)
            {
                var content = message.GetContent<ResultMessage>();
                if (content.Code != 200)
                {
                    task.TrySetException(new SpearException(content.Message, content.Code));
                }
                else
                {
                    task.SetResult(message);
                }
            }
            if (_executor != null && message.IsInvoke)
                await _executor.Execute(sender, message);
        }

        private async Task<T> RegistCallbackAsync<T>(string messageId)
        {
            _logger.LogDebug($"准备获取Id为：{messageId}的响应内容。");
            var task = new TaskCompletionSource<MicroMessage>();
            _resultDictionary.TryAdd(messageId, task);
            try
            {
                var result = await task.Task;
                return result.GetContent<T>();
            }
            finally
            {
                //删除回调任务
                _resultDictionary.TryRemove(messageId, out _);
            }
        }

        public async Task<T> Send<T>(object message)
        {
            try
            {
                _logger.LogDebug("准备发送消息");
                var microMessage = new MicroMessage(message);
                var callback = RegistCallbackAsync<T>(microMessage.Id);
                try
                {
                    _logger.LogDebug($"{_sender.GetType()}:send :{JsonConvert.SerializeObject(microMessage)}");
                    //发送
                    await _sender.Send(microMessage);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "与服务端通讯时发生了异常");
                    throw new SpearException("与服务端通讯时发生了异常");
                }
                _logger.LogDebug("消息发送成功");
                return await callback;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "消息发送失败。");
                throw new SpearException("消息发送失败");
            }
        }

        public async Task<ResultMessage> Send(InvokeMessage message)
        {
            return await Send<ResultMessage>(message);
        }

        public void Dispose()
        {
            (_sender as IDisposable)?.Dispose();
            (_listener as IDisposable)?.Dispose();
            foreach (var taskCompletionSource in _resultDictionary.Values)
            {
                taskCompletionSource.TrySetCanceled();
            }
        }
    }
}
