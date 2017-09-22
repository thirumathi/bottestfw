using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Bot.Builder.Internals.Fibers;
using Microsoft.Bot.Builder.Dialogs;

using Moq;
using Autofac;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Bot.Builder.Tests
{
    [TestClass]
    public abstract class FiberTestBase
    {
        public struct C
        {
        }

        public static readonly C Context = default(C);
        public static readonly CancellationToken Token = new CancellationTokenSource().Token;

        /// <summary>
        /// IMethod Interface 
        /// </summary>
        public interface IMethod
        {
            Task<IWait<C>> CodeAsync<T>(IFiber<C> fiber, C context, IAwaitable<T> item, CancellationToken token);
        }

        /// <summary>
        /// Mocks the method.
        /// </summary>
        /// <returns></returns>
        public static Moq.Mock<IMethod> MockMethod()
        {
            var method = new Moq.Mock<IMethod>(Moq.MockBehavior.Loose);
            return method;
        }

        /// <summary>
        /// Items the specified value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static Expression<Func<IAwaitable<T>, bool>> Item<T>(T value)
        {
            return item => value.Equals(item.GetAwaiter().GetResult());
        }

        protected sealed class CodeException : Exception
        {
        }

        /// <summary>
        /// Exceptions the type of the of.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        public static bool ExceptionOfType<T, E>(IAwaitable<T> item) where E : Exception
        {
            try
            {
                item.GetAwaiter().GetResult();
                return false;
            }
            catch (E)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Exceptions the type of the of.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static Expression<Func<IAwaitable<T>, bool>> ExceptionOfType<T, E>() where E : Exception
        {
            return item => ExceptionOfType<T, E>(item);
        }

        /// <summary>
        /// Polls the asynchronous.
        /// </summary>
        /// <param name="fiber">The fiber.</param>
        /// <returns></returns>
        public static async Task PollAsync(IFiberLoop<C> fiber)
        {
            IWait wait;
            do
            {
                wait = await fiber.PollAsync(Context, Token);
            }
            while (wait.Need != Need.None && wait.Need != Need.Done);
        }

        /// <summary>
        /// Builds this instance.
        /// </summary>
        /// <returns></returns>
        public static IContainer Build()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new FiberModule<C>());
            return builder.Build();
        }

        public sealed class ResolveMoqAssembly : IDisposable
        {
            private readonly object[] instances;
            /// <summary>
            /// Initializes a new instance of the <see cref="ResolveMoqAssembly"/> class.
            /// </summary>
            /// <param name="instances">The instances.</param>
            public ResolveMoqAssembly(params object[] instances)
            {
                SetField.NotNull(out this.instances, nameof(instances), instances);

                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }
            void IDisposable.Dispose()
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
            /// <summary>
            /// Handles the AssemblyResolve event of the CurrentDomain control.
            /// </summary>
            /// <param name="sender">The source of the event.</param>
            /// <param name="arguments">The <see cref="ResolveEventArgs"/> instance containing the event data.</param>
            /// <returns></returns>
            private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs arguments)
            {
                foreach (var instance in instances)
                {
                    var type = instance.GetType();
                    if (arguments.Name == type.Assembly.FullName)
                    {
                        return type.Assembly;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Asserts the serializable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="scope">The scope.</param>
        /// <param name="item">The item.</param>
        public static void AssertSerializable<T>(ILifetimeScope scope, ref T item) where T : class
        {
            var formatter = scope.Resolve<IFormatter>();

            using (var stream = new MemoryStream())
            {
                formatter.Serialize(stream, item);
                stream.Position = 0;
                item = (T)formatter.Deserialize(stream);
            }
        }
    }

}
