using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Wire.Compilation;

namespace Wire.ValueSerializers
{
    public abstract class SessionIgnorantValueSerializer<TElementType> : ValueSerializer
    {
        private readonly byte _manifest;
        private readonly MethodInfo _write;
        private readonly Action<Stream, object> _writeCompiled;
        private readonly MethodInfo _read;
        private readonly Func<Stream, TElementType> _readCompiled;

        protected SessionIgnorantValueSerializer(byte manifest,
            Expression<Func<Action<Stream, TElementType>>> writeStaticMethod,
            Expression<Func<Func<Stream, TElementType>>> readStaticMethod)
        {
            _manifest = manifest;
            _write = GetStatic(writeStaticMethod, typeof(void));
            _read = GetStatic(readStaticMethod, typeof(TElementType));

            var c = new IlCompiler<Action<Stream, object>>();

            var stream = c.Parameter<Stream>("stream");
            var value = c.Parameter<object>("value");
            var valueTyped = c.CastOrUnbox(value, typeof(TElementType));
            c.EmitStaticCall(_write, stream, valueTyped);

            _writeCompiled = c.Compile();

            var c2 = new IlCompiler<Func<Stream, TElementType>>();
            var stream2 = c2.Parameter<Stream>("stream");
            c2.EmitStaticCall(_read,stream2);

            _readCompiled = c2.Compile();
        }

        public sealed override void WriteManifest(Stream stream, SerializerSession session)
        {
            stream.WriteByte(_manifest);
        }

        public sealed override void WriteValue(Stream stream, object value, SerializerSession session)
        {
            _writeCompiled(stream, value);
        }

        public sealed override void EmitWriteValue(ICompiler<ObjectWriter> c, int stream, int fieldValue, int session)
        {
            c.EmitStaticCall(_write, stream, fieldValue);
        }

        public sealed override object ReadValue(Stream stream, DeserializerSession session)
        {
            return _readCompiled(stream);
        }

        public sealed override int EmitReadValue(ICompiler<ObjectReader> c, int stream, int session, FieldInfo field)
        {
            return c.StaticCall(_read, stream);
        }

        public sealed override Type GetElementType()
        {
            return typeof(TElementType);
        }
    }
}