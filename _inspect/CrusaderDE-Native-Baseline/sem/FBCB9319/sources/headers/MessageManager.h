class MessageManager
{
public:
	uint32_t IsQueueActive; //0x0000
	uint32_t ImmediateCommandId; //0x0004
	uint32_t ImmediateMessageType; //0x0008
	char pad_000C[200]; //0x000C
	uint32_t ImmediatePlayerId; //0x00D4
	char pad_00D8[4]; //0x00D8
	uint32_t QueueCommandIds[10]; //0x00DC
	uint32_t QueueMessageTypes[10]; //0x0104
	uint32_t QueueFlags[10]; //0x012C
	char QueueVideoPaths[10][100]; //0x0154
	char QueueAudioPaths[10][100]; //0x053C
	uint32_t QueuePlayerIds[10]; //0x0924
	uint32_t CurrentQueueCount; //0x094C
	char pad_0950[904]; //0x0950
}; //Size: 0x0CD8