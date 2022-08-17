using ZeroFrame.Protocol;
using ZeroFrame.Buffer;namespace Game.Protocol.Client2World
{

    [ZeroMessage]
    public class WolrdEnterReqMessage:IZeroMessage<WolrdEnterReqMessage>{
        public byte worldRoleTypeId;
        public byte worldFieldId;

        public void FromBytes(byte[] src, ref int offset)
        {
            worldRoleTypeId = BufferReader.ReadByte(src, ref offset);
            worldFieldId = BufferReader.ReadByte(src, ref offset);
            offset += src.Length;
        }

        public byte[] ToBytes()
        {
            int offset = 0;
            byte[] result = new byte[1000];
            BufferWriter.WriteByte(result, worldRoleTypeId, ref offset);
            BufferWriter.WriteByte(result, worldFieldId, ref offset);
            return result;
        }

    }

}