// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Cybermancy.Extensions;

namespace Cybermancy.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class RepeatTaskAttribute : Attribute
    {
        public TimeSpan TimeSpan { get; }

        public RepeatTaskAttribute(int days = 0, int hours = 0, int minutes = 0, int seconds = 0)
        {
            if (days < 0 || hours < 0 || minutes < 0 || seconds < 0) throw new ArgumentException("A parameter is less than 0");
            if (days == 0 && hours == 0 && minutes == 0 && seconds == 0) throw new ArgumentException("All parameters  equal to 0");
            this.TimeSpan = new TimeSpan(days, hours, minutes, seconds);
        }
    }

    public static class RepeatingTaskRegistration
    {

        public static IServiceProvider RegisterRepeatingTasks(this IServiceProvider services)
        {
            ServiceActivator.Configure(services);
            var methods = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(t => t.GetMethods())
                .Where(m => m.GetCustomAttributes(typeof(RepeatTaskAttribute), false).Length > 0)
                .ToArray();
            var cancellationTokenSource = new CancellationTokenSource();
            foreach (var method in methods)
            {
                var nullableAttribute = Attribute.GetCustomAttribute(method, typeof(RepeatTaskAttribute));
                if (nullableAttribute is not RepeatTaskAttribute attribute) continue;
                Task.Run(() => Repeat.IntervalAsync(
                    attribute.TimeSpan,
                    () => method.Invoke(null, null),
                    cancellationTokenSource.Token)
                );
            }
            return services;
        }

    }

    internal static class Repeat
    {
        public static Task IntervalAsync(
            TimeSpan pollInterval,
            Action action,
            CancellationToken token)
            => Task.Factory.StartNew(
                async () =>
                {
                    var periodicTimer = new PeriodicTimer(pollInterval);
                    while (await periodicTimer.WaitForNextTickAsync(token))
                        action();
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        
    }

    static class CancellationTokenExtensions
    {
        public static bool WaitCancellationRequested(
            this CancellationToken token,
            TimeSpan timeout) => token.WaitHandle.WaitOne(timeout);
    }
}
