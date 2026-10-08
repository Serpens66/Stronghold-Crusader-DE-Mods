
dispatcher: RVA 0x182B00, full extent 9137, role confidence candidate
00182B00  48895c2408                       mov qword ptr [rsp + 8], rbx
00182B05  48896c2410                       mov qword ptr [rsp + 0x10], rbp
00182B0A  4889742418                       mov qword ptr [rsp + 0x18], rsi
00182B0F  57                               push rdi
00182B10  4154                             push r12
00182B12  4155                             push r13
00182B14  4156                             push r14
00182B16  4157                             push r15
00182B18  4883ec30                         sub rsp, 0x30
00182B1C  0fbf35af7b3e08                   movsx esi, word ptr [rip + 0x83e7baf]
00182B23  488bd9                           mov rbx, rcx
00182B26  81e60f000080                     and esi, 0x8000000f
00182B2C  7d07                             jge 0x180182b35
00182B2E  ffce                             dec esi
00182B30  83cef0                           or esi, 0xfffffff0
00182B33  ffc6                             inc esi
00182B35  4c8d0db44a0505                   lea r9, [rip + 0x5054ab4]
00182B3C  4533c0                           xor r8d, r8d
00182B3F  ba20e50400                       mov edx, 0x4e520
00182B44  488d0dfdaaf205                   lea rcx, [rip + 0x5f2aafd]
00182B4B  e84049e8ff                       call 0x180007490
00182B50  33ed                             xor ebp, ebp
00182B52  4c8d35a7d4e7ff                   lea r14, [rip - 0x182b59]
00182B59  892d71bb6103                     mov dword ptr [rip + 0x361bb71], ebp
00182B5F  ba01000000                       mov edx, 1
00182B64  892d6aa56103                     mov dword ptr [rip + 0x361a56a], ebp
00182B6A  41bc1f030000                     mov r12d, 0x31f
00182B70  89ab14060000                     mov dword ptr [rbx + 0x614], ebp
00182B76  448bea                           mov r13d, edx
00182B79  66892d70ce8803                   mov word ptr [rip + 0x388ce70], bp
00182B80  448d4d03                         lea r9d, [rbp + 3]
00182B84  892d7ace8803                     mov dword ptr [rip + 0x388ce7a], ebp
00182B8A  448d7d6e                         lea r15d, [rbp + 0x6e]
00182B8E  892d78136203                     mov dword ptr [rip + 0x3621378], ebp
00182B94  892d76fd6103                     mov dword ptr [rip + 0x361fd76], ebp
00182B9A  89ab18060000                     mov dword ptr [rbx + 0x618], ebp
00182BA0  66892d4bce8803                   mov word ptr [rip + 0x388ce4b], bp
00182BA7  892d5bce8803                     mov dword ptr [rip + 0x388ce5b], ebp
00182BAD  892d956b6203                     mov dword ptr [rip + 0x3626b95], ebp
00182BB3  892d93556203                     mov dword ptr [rip + 0x3625593], ebp
00182BB9  89ab1c060000                     mov dword ptr [rbx + 0x61c], ebp
00182BBF  66892d2ece8803                   mov word ptr [rip + 0x388ce2e], bp
00182BC6  892d40ce8803                     mov dword ptr [rip + 0x388ce40], ebp
00182BCC  892db2c36203                     mov dword ptr [rip + 0x362c3b2], ebp
00182BD2  892db0ad6203                     mov dword ptr [rip + 0x362adb0], ebp
00182BD8  89ab20060000                     mov dword ptr [rbx + 0x620], ebp
00182BDE  66892d11ce8803                   mov word ptr [rip + 0x388ce11], bp
00182BE5  892d25ce8803                     mov dword ptr [rip + 0x388ce25], ebp
00182BEB  892dcf1b6303                     mov dword ptr [rip + 0x3631bcf], ebp
00182BF1  892dcd056303                     mov dword ptr [rip + 0x36305cd], ebp
00182BF7  89ab24060000                     mov dword ptr [rbx + 0x624], ebp
00182BFD  66892df4cd8803                   mov word ptr [rip + 0x388cdf4], bp
00182C04  892d0ace8803                     mov dword ptr [rip + 0x388ce0a], ebp
00182C0A  892dec736303                     mov dword ptr [rip + 0x36373ec], ebp
00182C10  892dea5d6303                     mov dword ptr [rip + 0x3635dea], ebp
00182C16  89ab28060000                     mov dword ptr [rbx + 0x628], ebp
00182C1C  66892dd7cd8803                   mov word ptr [rip + 0x388cdd7], bp
00182C23  892defcd8803                     mov dword ptr [rip + 0x388cdef], ebp
00182C29  892d09cc6303                     mov dword ptr [rip + 0x363cc09], ebp
00182C2F  892d07b66303                     mov dword ptr [rip + 0x363b607], ebp
00182C35  89ab2c060000                     mov dword ptr [rbx + 0x62c], ebp
00182C3B  66892dbacd8803                   mov word ptr [rip + 0x388cdba], bp
00182C42  892dd4cd8803                     mov dword ptr [rip + 0x388cdd4], ebp
00182C48  892d26246403                     mov dword ptr [rip + 0x3642426], ebp
00182C4E  892d240e6403                     mov dword ptr [rip + 0x3640e24], ebp
00182C54  89ab30060000                     mov dword ptr [rbx + 0x630], ebp
00182C5A  66892d9dcd8803                   mov word ptr [rip + 0x388cd9d], bp
00182C61  892db9cd8803                     mov dword ptr [rip + 0x388cdb9], ebp
00182C67  892d437c6403                     mov dword ptr [rip + 0x3647c43], ebp
00182C6D  892d41666403                     mov dword ptr [rip + 0x3646641], ebp
00182C73  89ab34060000                     mov dword ptr [rbx + 0x634], ebp
00182C79  66892d80cd8803                   mov word ptr [rip + 0x388cd80], bp
00182C80  892d9ecd8803                     mov dword ptr [rip + 0x388cd9e], ebp
00182C86  48892b                           mov qword ptr [rbx], rbp
00182C89  892d25f66e03                     mov dword ptr [rip + 0x36ef625], ebp
00182C8F  89152fd67a00                     mov dword ptr [rip + 0x7ad62f], edx
00182C95  4863c2                           movsxd rax, edx
00182C98  4869c890040000                   imul rcx, rax, 0x490
00182C9F  6639ac19e4060000                 cmp word ptr [rcx + rbx + 0x6e4], bp
00182CA7  0f8492040000                     je 0x18018313f
00182CAD  8d4201                           lea eax, [rdx + 1]
00182CB0  8903                             mov dword ptr [rbx], eax
00182CB2  4863050bd67a00                   movsxd rax, dword ptr [rip + 0x7ad60b]
00182CB9  4869c890040000                   imul rcx, rax, 0x490
00182CC0  6689ac19f2090000                 mov word ptr [rcx + rbx + 0x9f2], bp
00182CC8  486305f5d57a00                   movsxd rax, dword ptr [rip + 0x7ad5f5]
00182CCF  4869c890040000                   imul rcx, rax, 0x490
00182CD6  6689ac19060a0000                 mov word ptr [rcx + rbx + 0xa06], bp
00182CDE  486305dfd57a00                   movsxd rax, dword ptr [rip + 0x7ad5df]
00182CE5  4869c890040000                   imul rcx, rax, 0x490
00182CEC  6689ac190a0a0000                 mov word ptr [rcx + rbx + 0xa0a], bp
00182CF4  486305c9d57a00                   movsxd rax, dword ptr [rip + 0x7ad5c9]
00182CFB  4869c890040000                   imul rcx, rax, 0x490
00182D02  6689ac19340a0000                 mov word ptr [rcx + rbx + 0xa34], bp
00182D0A  486305b3d57a00                   movsxd rax, dword ptr [rip + 0x7ad5b3]
00182D11  4869c890040000                   imul rcx, rax, 0x490
00182D18  4088ac19b40a0000                 mov byte ptr [rcx + rbx + 0xab4], bpl
00182D20  4863059dd57a00                   movsxd rax, dword ptr [rip + 0x7ad59d]
00182D27  4869c890040000                   imul rcx, rax, 0x490
00182D2E  89ac19bc060000                   mov dword ptr [rcx + rbx + 0x6bc], ebp
00182D35  48633d88d57a00                   movsxd rdi, dword ptr [rip + 0x7ad588]
00182D3C  4869cf90040000                   imul rcx, rdi, 0x490
00182D43  0fb7841902090000                 movzx eax, word ptr [rcx + rbx + 0x902]
00182D4B  6685c0                           test ax, ax
00182D4E  7411                             je 0x180182d61
00182D50  66ffc8                           dec ax
00182D53  6689841902090000                 mov word ptr [rcx + rbx + 0x902], ax
00182D5B  8b3d63d57a00                     mov edi, dword ptr [rip + 0x7ad563]
00182D61  4863c7                           movsxd rax, edi
00182D64  4869c890040000                   imul rcx, rax, 0x490
00182D6B  6639ac1918090000                 cmp word ptr [rcx + rbx + 0x918], bp
00182D73  7534                             jne 0x180182da9
00182D75  8bd7                             mov edx, edi
00182D77  488bcb                           mov rcx, rbx
00182D7A  e8f1f6ffff                       call 0x180182470
00182D7F  41b903000000                     mov r9d, 3
00182D85  85c0                             test eax, eax
00182D87  7520                             jne 0x180182da9
00182D89  ff4318                           inc dword ptr [rbx + 0x18]
00182D8C  48630531d57a00                   movsxd rax, dword ptr [rip + 0x7ad531]
00182D93  4869c890040000                   imul rcx, rax, 0x490
00182D9A  6644898c19e4060000               mov word ptr [rcx + rbx + 0x6e4], r9w
00182DA3  8b3d1bd57a00                     mov edi, dword ptr [rip + 0x7ad51b]
00182DA9  4863c7                           movsxd rax, edi
00182DAC  4869c890040000                   imul rcx, rax, 0x490
00182DB3  486394192c070000                 movsxd rdx, dword ptr [rcx + rbx + 0x72c]
00182DBB  81fa1fe50400                     cmp edx, 0x4e51f
00182DC1  7743                             ja 0x180182e06
00182DC3  4d0fbf8456a4e2aa03               movsx r8, word ptr [r14 + rdx*2 + 0x3aae2a4]
00182DCC  498bc8                           mov rcx, r8
00182DCF  4b8d0440                         lea rax, [r8 + r8*2]
00182DD3  412b94862cff0204                 sub edx, dword ptr [r14 + rax*4 + 0x402ff2c]
00182DDB  413bd4                           cmp edx, r12d
00182DDE  7720                             ja 0x180182e00
00182DE0  66453bc4                         cmp r8w, r12w
00182DE4  771a                             ja 0x180182e00
00182DE6  4869c920030000                   imul rcx, rcx, 0x320
00182DED  4863c2                           movsxd rax, edx
00182DF0  4803c8                           add rcx, rax
00182DF3  418bc5                           mov eax, r13d
00182DF6  4238ac31a41ea103                 cmp byte ptr [rcx + r14 + 0x3a11ea4], bpl
00182DFE  7502                             jne 0x180182e02
00182E00  8bc5                             mov eax, ebp
00182E02  85c0                             test eax, eax
00182E04  7520                             jne 0x180182e26
00182E06  ff4318                           inc dword ptr [rbx + 0x18]
00182E09  486305b4d47a00                   movsxd rax, dword ptr [rip + 0x7ad4b4]
00182E10  4869c890040000                   imul rcx, rax, 0x490
00182E17  6644898c19e4060000               mov word ptr [rcx + rbx + 0x6e4], r9w
00182E20  8b3d9ed47a00                     mov edi, dword ptr [rip + 0x7ad49e]
00182E26  4863c7                           movsxd rax, edi
00182E29  4869c890040000                   imul rcx, rax, 0x490
00182E30  4c0fbf8419ee060000               movsx r8, word ptr [rcx + rbx + 0x6ee]
00182E39  4383bc86cc4b5708ff               cmp dword ptr [r14 + r8*4 + 0x8574bcc], -1
00182E42  7405                             je 0x180182e49
00182E44  0fb7c5                           movzx eax, bp
00182E47  eb10                             jmp 0x180182e59
00182E49  4969c03c580000                   imul rax, r8, 0x583c
00182E50  420fb78430a8d97903               movzx eax, word ptr [rax + r14 + 0x379d9a8]
00182E59  668984198a0a0000                 mov word ptr [rcx + rbx + 0xa8a], ax
00182E61  4863155cd47a00                   movsxd rdx, dword ptr [rip + 0x7ad45c]
00182E68  4869ca90040000                   imul rcx, rdx, 0x490
00182E6F  8b84193c0a0000                   mov eax, dword ptr [rcx + rbx + 0xa3c]
00182E76  85c0                             test eax, eax
00182E78  7420                             je 0x180182e9a
00182E7A  ffc8                             dec eax
00182E7C  8984193c0a0000                   mov dword ptr [rcx + rbx + 0xa3c], eax
00182E83  8b153bd47a00                     mov edx, dword ptr [rip + 0x7ad43b]
00182E89  4969c03c580000                   imul rax, r8, 0x583c
00182E90  42ff843008d07903                 inc dword ptr [rax + r14 + 0x379d008]
00182E98  eb22                             jmp 0x180182ebc
00182E9A  480fbf8419e6060000               movsx rax, word ptr [rcx + rbx + 0x6e6]
00182EA3  4139ac86f0b73200                 cmp dword ptr [r14 + rax*4 + 0x32b7f0], ebp
00182EAB  750f                             jne 0x180182ebc
00182EAD  4969c03c580000                   imul rax, r8, 0x583c
00182EB4  42ff84300cd07903                 inc dword ptr [rax + r14 + 0x379d00c]
00182EBC  8bc2                             mov eax, edx
00182EBE  250f000080                       and eax, 0x8000000f
00182EC3  7d07                             jge 0x180182ecc
00182EC5  ffc8                             dec eax
00182EC7  83c8f0                           or eax, 0xfffffff0
00182ECA  ffc0                             inc eax
00182ECC  3bc6                             cmp eax, esi
00182ECE  0f850e020000                     jne 0x1801830e2
00182ED4  4863c2                           movsxd rax, edx
00182ED7  4869c890040000                   imul rcx, rax, 0x490
00182EDE  486384192c070000                 movsxd rax, dword ptr [rcx + rbx + 0x72c]
00182EE6  664139ac4690c60e05               cmp word ptr [r14 + rax*2 + 0x50ec690], bp
00182EEF  0f8594010000                     jne 0x180183089
00182EF5  41f78486b0718f0400000040         test dword ptr [r14 + rax*4 + 0x48f71b0], 0x40000000
00182F01  0f8582010000                     jne 0x180183089
00182F07  664439ac19ec060000               cmp word ptr [rcx + rbx + 0x6ec], r13w
00182F10  0f8473010000                     je 0x180183089
00182F16  490fbf844650aab604               movsx rax, word ptr [r14 + rax*2 + 0x4b6aa50]
00182F1F  6685c0                           test ax, ax
00182F22  7439                             je 0x180182f5d
00182F24  4c69c02c030000                   imul r8, rax, 0x32c
00182F2B  664383bc30dccc4c0602             cmp word ptr [r8 + r14 + 0x64cccdc], 2
00182F35  7519                             jne 0x180182f50
00182F37  4f0fbf8430decc4c06               movsx r8, word ptr [r8 + r14 + 0x64cccde]
00182F40  4339ac86d0682e00                 cmp dword ptr [r14 + r8*4 + 0x2e68d0], ebp
00182F48  7506                             jne 0x180182f50
00182F4A  4183f831                         cmp r8d, 0x31
00182F4E  750d                             jne 0x180182f5d
00182F50  6689ac19be0a0000                 mov word ptr [rcx + rbx + 0xabe], bp
00182F58  e926010000                       jmp 0x180183083
00182F5D  664439bc1918090000               cmp word ptr [rcx + rbx + 0x918], r15w
00182F66  0f841d010000                     je 0x180183089
00182F6C  6683bc19e60600003e               cmp word ptr [rcx + rbx + 0x6e6], 0x3e
00182F75  752a                             jne 0x180182fa1
00182F77  460fbf84311e8b7e06               movsx r8d, word ptr [rcx + r14 + 0x67e8b1e]
00182F80  41b98a000000                     mov r9d, 0x8a
00182F86  420fbf94311c8b7e06               movsx edx, word ptr [rcx + r14 + 0x67e8b1c]
00182F8F  488d0dfa143406                   lea rcx, [rip + 0x63414fa]
00182F96  e8c5f9e7ff                       call 0x180002960
00182F9B  8b1523d37a00                     mov edx, dword ptr [rip + 0x7ad323]
00182FA1  4863c2                           movsxd rax, edx
00182FA4  4869c890040000                   imul rcx, rax, 0x490
00182FAB  8b05a72f4e03                     mov eax, dword ptr [rip + 0x34e2fa7]
00182FB1  2b8419f4060000                   sub eax, dword ptr [rcx + rbx + 0x6f4]
00182FB8  3de8030000                       cmp eax, 0x3e8
00182FBD  0f8cc6000000                     jl 0x180183089
00182FC3  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
00182FCB  6683f83a                         cmp ax, 0x3a
00182FCF  0f84b4000000                     je 0x180183089
00182FD5  6683f837                         cmp ax, 0x37
00182FD9  7568                             jne 0x180183043
00182FDB  460fbf8431928d7e06               movsx r8d, word ptr [rcx + r14 + 0x67e8d92]
00182FE4  4e0fbf8c31948d7e06               movsx r9, word ptr [rcx + r14 + 0x67e8d94]
00182FED  4b8d0c49                         lea rcx, [r9 + r9*2]
00182FF1  418b848e2cff0204                 mov eax, dword ptr [r14 + rcx*4 + 0x402ff2c]
00182FF9  4103c0                           add eax, r8d
00182FFC  4863c8                           movsxd rcx, eax
00182FFF  420fb6843150d3dd04               movzx eax, byte ptr [rcx + r14 + 0x4ddd350]
00183008  488bcb                           mov rcx, rbx
0018300B  89442420                         mov dword ptr [rsp + 0x20], eax
0018300F  e82c8a0100                       call 0x18019ba40
00183014  486305a9d27a00                   movsxd rax, dword ptr [rip + 0x7ad2a9]
0018301B  4869c890040000                   imul rcx, rax, 0x490
00183022  664489ac1918090000               mov word ptr [rcx + rbx + 0x918], r13w
0018302B  48630592d27a00                   movsxd rax, dword ptr [rip + 0x7ad292]
00183032  4869c890040000                   imul rcx, rax, 0x490
00183039  6689ac19cc090000                 mov word ptr [rcx + rbx + 0x9cc], bp
00183041  eb40                             jmp 0x180183083
00183043  66ff8419be0a0000                 inc word ptr [rcx + rbx + 0xabe]
0018304B  48631572d27a00                   movsxd rdx, dword ptr [rip + 0x7ad272]
00183052  4869ca90040000                   imul rcx, rdx, 0x490
00183059  6683bc19be0a00000d               cmp word ptr [rcx + rbx + 0xabe], 0xd
00183062  7e25                             jle 0x180183089
00183064  664489bc1918090000               mov word ptr [rcx + rbx + 0x918], r15w
0018306D  48630550d27a00                   movsxd rax, dword ptr [rip + 0x7ad250]
00183074  4883c002                         add rax, 2
00183078  4869c890040000                   imul rcx, rax, 0x490
0018307F  66892c19                         mov word ptr [rcx + rbx], bp
00183083  8b153bd27a00                     mov edx, dword ptr [rip + 0x7ad23b]
00183089  4863c2                           movsxd rax, edx
0018308C  4869c890040000                   imul rcx, rax, 0x490
00183093  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
0018309B  6683f827                         cmp ax, 0x27
0018309F  7406                             je 0x1801830a7
001830A1  6683f84d                         cmp ax, 0x4d
001830A5  753b                             jne 0x1801830e2
001830A7  486384192c070000                 movsxd rax, dword ptr [rcx + rbx + 0x72c]
001830AF  41f78486b0718f0400000050         test dword ptr [r14 + rax*4 + 0x48f71b0], 0x50000000
001830BB  7425                             je 0x1801830e2
001830BD  664489bc1918090000               mov word ptr [rcx + rbx + 0x918], r15w
001830C6  486305f7d17a00                   movsxd rax, dword ptr [rip + 0x7ad1f7]
001830CD  4883c002                         add rax, 2
001830D1  4869c890040000                   imul rcx, rax, 0x490
001830D8  66892c19                         mov word ptr [rcx + rbx], bp
001830DC  8b15e2d17a00                     mov edx, dword ptr [rip + 0x7ad1e2]
001830E2  4863c2                           movsxd rax, edx
001830E5  4869c890040000                   imul rcx, rax, 0x490
001830EC  6683bc19e406000002               cmp word ptr [rcx + rbx + 0x6e4], 2
001830F5  7542                             jne 0x180183139
001830F7  6639ac191c070000                 cmp word ptr [rcx + rbx + 0x71c], bp
001830FF  7413                             je 0x180183114
00183101  6639ac191e070000                 cmp word ptr [rcx + rbx + 0x71e], bp
00183109  7409                             je 0x180183114
0018310B  39ac192c070000                   cmp dword ptr [rcx + rbx + 0x72c], ebp
00183112  7d25                             jge 0x180183139
00183114  664489bc1918090000               mov word ptr [rcx + rbx + 0x918], r15w
0018311D  486305a0d17a00                   movsxd rax, dword ptr [rip + 0x7ad1a0]
00183124  4883c002                         add rax, 2
00183128  4869c890040000                   imul rcx, rax, 0x490
0018312F  66892c19                         mov word ptr [rcx + rbx], bp
00183133  8b158bd17a00                     mov edx, dword ptr [rip + 0x7ad18b]
00183139  41b903000000                     mov r9d, 3
0018313F  ffc2                             inc edx
00183141  89157dd17a00                     mov dword ptr [rip + 0x7ad17d], edx
00183147  81fa10270000                     cmp edx, 0x2710
0018314D  0f8c42fbffff                     jl 0x180182c95
00183153  418bd5                           mov edx, r13d
00183156  418bfd                           mov edi, r13d
00183159  891565d17a00                     mov dword ptr [rip + 0x7ad165], edx
0018315F  44392b                           cmp dword ptr [rbx], r13d
00183162  0f8e7c010000                     jle 0x1801832e4
00183168  0f1f840000000000                 nop dword ptr [rax + rax]
00183170  4863c2                           movsxd rax, edx
00183173  4869c890040000                   imul rcx, rax, 0x490
0018317A  6639ac19e4060000                 cmp word ptr [rcx + rbx + 0x6e4], bp
00183182  0f844c010000                     je 0x1801832d4
00183188  6683bc19180900006a               cmp word ptr [rcx + rbx + 0x918], 0x6a
00183191  756a                             jne 0x1801831fd
00183193  480fbf841996090000               movsx rax, word ptr [rcx + rbx + 0x996]
0018319C  4869c890040000                   imul rcx, rax, 0x490
001831A3  66ff8419060a0000                 inc word ptr [rcx + rbx + 0xa06]
001831AB  4c8d0419                         lea r8, [rcx + rbx]
001831AF  410fbf8096090000                 movsx eax, word ptr [r8 + 0x996]
001831B7  48631506d17a00                   movsxd rdx, dword ptr [rip + 0x7ad106]
001831BE  440fb78c19060a0000               movzx r9d, word ptr [rcx + rbx + 0xa06]
001831C7  3bc2                             cmp eax, edx
001831C9  741d                             je 0x1801831e8
001831CB  4869ca90040000                   imul rcx, rdx, 0x490
001831D2  66ff8419060a0000                 inc word ptr [rcx + rbx + 0xa06]
001831DA  450fb788060a0000                 movzx r9d, word ptr [r8 + 0xa06]
001831E2  8b15dcd07a00                     mov edx, dword ptr [rip + 0x7ad0dc]
001831E8  66453bcd                         cmp r9w, r13w
001831EC  750f                             jne 0x1801831fd
001831EE  4138a884090000                   cmp byte ptr [r8 + 0x984], bpl
001831F5  7506                             jne 0x1801831fd
001831F7  ff054b2d4e03                     inc dword ptr [rip + 0x34e2d4b]
001831FD  4863c2                           movsxd rax, edx
00183200  4869c890040000                   imul rcx, rax, 0x490
00183207  480fbf84199e090000               movsx rax, word ptr [rcx + rbx + 0x99e]
00183210  6685c0                           test ax, ax
00183213  7415                             je 0x18018322a
00183215  4869c890040000                   imul rcx, rax, 0x490
0018321C  66ff84190a0a0000                 inc word ptr [rcx + rbx + 0xa0a]
00183224  8b159ad07a00                     mov edx, dword ptr [rip + 0x7ad09a]
0018322A  4863c2                           movsxd rax, edx
0018322D  4869c890040000                   imul rcx, rax, 0x490
00183234  480fbf8419360a0000               movsx rax, word ptr [rcx + rbx + 0xa36]
0018323D  6685c0                           test ax, ax
00183240  7415                             je 0x180183257
00183242  4869c890040000                   imul rcx, rax, 0x490
00183249  66ff8419340a0000                 inc word ptr [rcx + rbx + 0xa34]
00183251  8b156dd07a00                     mov edx, dword ptr [rip + 0x7ad06d]
00183257  4863c2                           movsxd rax, edx
0018325A  4869c890040000                   imul rcx, rax, 0x490
00183261  0fb7841928070000                 movzx eax, word ptr [rcx + rbx + 0x728]
00183269  6685c0                           test ax, ax
0018326C  7e11                             jle 0x18018327f
0018326E  66ffc8                           dec ax
00183271  6689841928070000                 mov word ptr [rcx + rbx + 0x728], ax
00183279  8b1545d07a00                     mov edx, dword ptr [rip + 0x7ad045]
0018327F  4863c2                           movsxd rax, edx
00183282  4869c890040000                   imul rcx, rax, 0x490
00183289  0fb78419b80a0000                 movzx eax, word ptr [rcx + rbx + 0xab8]
00183291  6685c0                           test ax, ax
00183294  7e11                             jle 0x1801832a7
00183296  66ffc8                           dec ax
00183299  66898419b80a0000                 mov word ptr [rcx + rbx + 0xab8], ax
001832A1  8b151dd07a00                     mov edx, dword ptr [rip + 0x7ad01d]
001832A7  4863c2                           movsxd rax, edx
001832AA  4869c890040000                   imul rcx, rax, 0x490
001832B1  6683bc19e60600001e               cmp word ptr [rcx + rbx + 0x6e6], 0x1e
001832BA  7518                             jne 0x1801832d4
001832BC  6683bc191809000005               cmp word ptr [rcx + rbx + 0x918], 5
001832C5  750d                             jne 0x1801832d4
001832C7  ff841974090000                   inc dword ptr [rcx + rbx + 0x974]
001832CE  8b15f0cf7a00                     mov edx, dword ptr [rip + 0x7acff0]
001832D4  ffc2                             inc edx
001832D6  8915e8cf7a00                     mov dword ptr [rip + 0x7acfe8], edx
001832DC  3b13                             cmp edx, dword ptr [rbx]
001832DE  0f8c8cfeffff                     jl 0x180183170
001832E4  8b050a0c6203                     mov eax, dword ptr [rip + 0x3620c0a]
001832EA  0f57c0                           xorps xmm0, xmm0
001832ED  8b0d652c4e03                     mov ecx, dword ptr [rip + 0x34e2c65]
001832F3  0f57c9                           xorps xmm1, xmm1
001832F6  83e13f                           and ecx, 0x3f
001832F9  48892df0036203                   mov qword ptr [rip + 0x36203f0], rbp
00183300  892d72fe6103                     mov dword ptr [rip + 0x361fe72], ebp
00183306  0f44c5                           cmove eax, ebp
00183309  48892de8036203                   mov qword ptr [rip + 0x36203e8], rbp
00183310  8905de0b6203                     mov dword ptr [rip + 0x3620bde], eax
00183316  85c9                             test ecx, ecx
00183318  8b0512646203                     mov eax, dword ptr [rip + 0x3626412]
0018331E  0f44c5                           cmove eax, ebp
00183321  892d210c6203                     mov dword ptr [rip + 0x3620c21], ebp
00183327  890503646203                     mov dword ptr [rip + 0x3626403], eax
0018332D  8b0539bc6203                     mov eax, dword ptr [rip + 0x362bc39]
00183333  0f44c5                           cmove eax, ebp
00183336  892d140c6203                     mov dword ptr [rip + 0x3620c14], ebp
0018333C  89052abc6203                     mov dword ptr [rip + 0x362bc2a], eax
00183342  8b0560146303                     mov eax, dword ptr [rip + 0x3631460]
00183348  0f44c5                           cmove eax, ebp
0018334B  48892d12fe6103                   mov qword ptr [rip + 0x361fe12], rbp
00183352  890550146303                     mov dword ptr [rip + 0x3631450], eax
00183358  8b05866c6303                     mov eax, dword ptr [rip + 0x3636c86]
0018335E  0f44c5                           cmove eax, ebp
00183361  892d4dfe6103                     mov dword ptr [rip + 0x361fe4d], ebp
00183367  8905776c6303                     mov dword ptr [rip + 0x3636c77], eax
0018336D  892d650c6203                     mov dword ptr [rip + 0x3620c65], ebp
00183373  f30f7f053dfe6103                 movdqu xmmword ptr [rip + 0x361fe3d], xmm0
0018337B  48892d56fe6103                   mov qword ptr [rip + 0x361fe56], rbp
00183382  f30f7f0d3efe6103                 movdqu xmmword ptr [rip + 0x361fe3e], xmm1
0018338A  892d980b6203                     mov dword ptr [rip + 0x3620b98], ebp
00183390  48892d955b6203                   mov qword ptr [rip + 0x3625b95], rbp
00183397  892d17566203                     mov dword ptr [rip + 0x3625617], ebp
0018339D  48892d905b6203                   mov qword ptr [rip + 0x3625b90], rbp
001833A4  892dda636203                     mov dword ptr [rip + 0x36263da], ebp
001833AA  892ddc636203                     mov dword ptr [rip + 0x36263dc], ebp
001833B0  48892de9556203                   mov qword ptr [rip + 0x36255e9], rbp
001833B7  892d33566203                     mov dword ptr [rip + 0x3625633], ebp
001833BD  892d51646203                     mov dword ptr [rip + 0x3626451], ebp
001833C3  f30f7f0529566203                 movdqu xmmword ptr [rip + 0x3625629], xmm0
001833CB  48892d42566203                   mov qword ptr [rip + 0x3625642], rbp
001833D2  f30f7f0d2a566203                 movdqu xmmword ptr [rip + 0x362562a], xmm1
001833DA  892d84636203                     mov dword ptr [rip + 0x3626384], ebp
001833E0  48892d81b36203                   mov qword ptr [rip + 0x362b381], rbp
001833E7  892d03ae6203                     mov dword ptr [rip + 0x362ae03], ebp
001833ED  48892d7cb36203                   mov qword ptr [rip + 0x362b37c], rbp
001833F4  892dc6bb6203                     mov dword ptr [rip + 0x362bbc6], ebp
001833FA  892dc8bb6203                     mov dword ptr [rip + 0x362bbc8], ebp
00183400  48892dd5ad6203                   mov qword ptr [rip + 0x362add5], rbp
00183407  892d1fae6203                     mov dword ptr [rip + 0x362ae1f], ebp
0018340D  892d3dbc6203                     mov dword ptr [rip + 0x362bc3d], ebp
00183413  f30f7f0515ae6203                 movdqu xmmword ptr [rip + 0x362ae15], xmm0
0018341B  48892d2eae6203                   mov qword ptr [rip + 0x362ae2e], rbp
00183422  f30f7f0d16ae6203                 movdqu xmmword ptr [rip + 0x362ae16], xmm1
0018342A  892d70bb6203                     mov dword ptr [rip + 0x362bb70], ebp
00183430  48892d6d0b6303                   mov qword ptr [rip + 0x3630b6d], rbp
00183437  892def056303                     mov dword ptr [rip + 0x36305ef], ebp
0018343D  48892d680b6303                   mov qword ptr [rip + 0x3630b68], rbp
00183444  892db2136303                     mov dword ptr [rip + 0x36313b2], ebp
0018344A  892db4136303                     mov dword ptr [rip + 0x36313b4], ebp
00183450  48892dc1056303                   mov qword ptr [rip + 0x36305c1], rbp
00183457  892d0b066303                     mov dword ptr [rip + 0x363060b], ebp
0018345D  892d29146303                     mov dword ptr [rip + 0x3631429], ebp
00183463  f30f7f0501066303                 movdqu xmmword ptr [rip + 0x3630601], xmm0
0018346B  48892d1a066303                   mov qword ptr [rip + 0x363061a], rbp
00183472  f30f7f0d02066303                 movdqu xmmword ptr [rip + 0x3630602], xmm1
0018347A  892d5c136303                     mov dword ptr [rip + 0x363135c], ebp
00183480  48892d59636303                   mov qword ptr [rip + 0x3636359], rbp
00183487  892ddb5d6303                     mov dword ptr [rip + 0x3635ddb], ebp
0018348D  48892d54636303                   mov qword ptr [rip + 0x3636354], rbp
00183494  892d9e6b6303                     mov dword ptr [rip + 0x3636b9e], ebp
0018349A  892da06b6303                     mov dword ptr [rip + 0x3636ba0], ebp
001834A0  48892dad5d6303                   mov qword ptr [rip + 0x3635dad], rbp
001834A7  892df75d6303                     mov dword ptr [rip + 0x3635df7], ebp
001834AD  892d156c6303                     mov dword ptr [rip + 0x3636c15], ebp
001834B3  f30f7f05ed5d6303                 movdqu xmmword ptr [rip + 0x3635ded], xmm0
001834BB  48892d065e6303                   mov qword ptr [rip + 0x3635e06], rbp
001834C2  f30f7f0dee5d6303                 movdqu xmmword ptr [rip + 0x3635dee], xmm1
001834CA  892d486b6303                     mov dword ptr [rip + 0x3636b48], ebp
001834D0  48892d45bb6303                   mov qword ptr [rip + 0x363bb45], rbp
001834D7  8b0543c36303                     mov eax, dword ptr [rip + 0x363c343]
001834DD  418bd5                           mov edx, r13d
001834E0  0f44c5                           cmove eax, ebp
001834E3  892dbbb56303                     mov dword ptr [rip + 0x363b5bb], ebp
001834E9  890531c36303                     mov dword ptr [rip + 0x363c331], eax
001834EF  8b05671b6403                     mov eax, dword ptr [rip + 0x3641b67]
001834F5  0f44c5                           cmove eax, ebp
001834F8  48892d25bb6303                   mov qword ptr [rip + 0x363bb25], rbp
001834FF  8905571b6403                     mov dword ptr [rip + 0x3641b57], eax
00183505  8b058d736403                     mov eax, dword ptr [rip + 0x364738d]
0018350B  0f44c5                           cmove eax, ebp
0018350E  892d60c36303                     mov dword ptr [rip + 0x363c360], ebp
00183514  89057e736403                     mov dword ptr [rip + 0x364737e], eax
0018351A  892d5cc36303                     mov dword ptr [rip + 0x363c35c], ebp
00183520  48892d69b56303                   mov qword ptr [rip + 0x363b569], rbp
00183527  892db3b56303                     mov dword ptr [rip + 0x363b5b3], ebp
0018352D  892dd1c36303                     mov dword ptr [rip + 0x363c3d1], ebp
00183533  f30f7f05a9b56303                 movdqu xmmword ptr [rip + 0x363b5a9], xmm0
0018353B  48892dc2b56303                   mov qword ptr [rip + 0x363b5c2], rbp
00183542  f30f7f0daab56303                 movdqu xmmword ptr [rip + 0x363b5aa], xmm1
0018354A  892d04c36303                     mov dword ptr [rip + 0x363c304], ebp
00183550  48892d01136403                   mov qword ptr [rip + 0x3641301], rbp
00183557  892d830d6403                     mov dword ptr [rip + 0x3640d83], ebp
0018355D  48892dfc126403                   mov qword ptr [rip + 0x36412fc], rbp
00183564  892d461b6403                     mov dword ptr [rip + 0x3641b46], ebp
0018356A  892d481b6403                     mov dword ptr [rip + 0x3641b48], ebp
00183570  48892d550d6403                   mov qword ptr [rip + 0x3640d55], rbp
00183577  892d9f0d6403                     mov dword ptr [rip + 0x3640d9f], ebp
0018357D  892dbd1b6403                     mov dword ptr [rip + 0x3641bbd], ebp
00183583  f30f7f05950d6403                 movdqu xmmword ptr [rip + 0x3640d95], xmm0
0018358B  48892dae0d6403                   mov qword ptr [rip + 0x3640dae], rbp
00183592  f30f7f0d960d6403                 movdqu xmmword ptr [rip + 0x3640d96], xmm1
0018359A  892df01a6403                     mov dword ptr [rip + 0x3641af0], ebp
001835A0  48892ded6a6403                   mov qword ptr [rip + 0x3646aed], rbp
001835A7  892d6f656403                     mov dword ptr [rip + 0x364656f], ebp
001835AD  48892de86a6403                   mov qword ptr [rip + 0x3646ae8], rbp
001835B4  892d32736403                     mov dword ptr [rip + 0x3647332], ebp
001835BA  892d34736403                     mov dword ptr [rip + 0x3647334], ebp
001835C0  48892d41656403                   mov qword ptr [rip + 0x3646541], rbp
001835C7  892d8b656403                     mov dword ptr [rip + 0x364658b], ebp
001835CD  892da9736403                     mov dword ptr [rip + 0x36473a9], ebp
001835D3  f30f7f0581656403                 movdqu xmmword ptr [rip + 0x3646581], xmm0
001835DB  48892d9a656403                   mov qword ptr [rip + 0x364659a], rbp
001835E2  f30f7f0d82656403                 movdqu xmmword ptr [rip + 0x3646582], xmm1
001835EA  892ddc726403                     mov dword ptr [rip + 0x36472dc], ebp
001835F0  8915cecc7a00                     mov dword ptr [rip + 0x7accce], edx
001835F6  44392b                           cmp dword ptr [rbx], r13d
001835F9  0f8e01060000                     jle 0x180183c00
001835FF  48be0100000000000110             movabs rsi, 0x1001000000000001
00183609  0f1f8000000000                   nop dword ptr [rax]
00183610  4863ca                           movsxd rcx, edx
00183613  4869c190040000                   imul rax, rcx, 0x490
0018361A  6639ac18e4060000                 cmp word ptr [rax + rbx + 0x6e4], bp
00183622  0f84c8050000                     je 0x180183bf0
00183628  488d04c9                         lea rax, [rcx + rcx*8]
0018362C  4088ac43583fb200                 mov byte ptr [rbx + rax*2 + 0xb23f58], bpl
00183634  ff4304                           inc dword ptr [rbx + 4]
00183637  48631586cc7a00                   movsxd rdx, dword ptr [rip + 0x7acc86]
0018363E  4869ca90040000                   imul rcx, rdx, 0x490
00183645  6639ac19e8060000                 cmp word ptr [rcx + rbx + 0x6e8], bp
0018364D  740e                             je 0x18018365d
0018364F  488bcb                           mov rcx, rbx
00183652  e8f9c7ffff                       call 0x18017fe50
00183657  8b1567cc7a00                     mov edx, dword ptr [rip + 0x7acc67]
0018365D  4863c2                           movsxd rax, edx
00183660  4c69c090040000                   imul r8, rax, 0x490
00183667  4c03c3                           add r8, rbx
0018366A  4963882c070000                   movsxd rcx, dword ptr [r8 + 0x72c]
00183671  410fb680280a0000                 movzx eax, byte ptr [r8 + 0xa28]
00183679  42088431f0751d05                 or byte ptr [rcx + r14 + 0x51d75f0], al
00183681  664183b8e606000037               cmp word ptr [r8 + 0x6e6], 0x37
0018368A  7528                             jne 0x1801836b4
0018368C  490fbf80ee060000                 movsx rax, word ptr [r8 + 0x6ee]
00183694  6641899446f0f9a003               mov word ptr [r14 + rax*2 + 0x3a0f9f0], dx
0018369D  490fbf88ee060000                 movsx rcx, word ptr [r8 + 0x6ee]
001836A5  418b80f0060000                   mov eax, dword ptr [r8 + 0x6f0]
001836AC  4189848e04faa003                 mov dword ptr [r14 + rcx*4 + 0x3a0fa04], eax
001836B4  664139a8fc080000                 cmp word ptr [r8 + 0x8fc], bp
001836BC  0f842b030000                     je 0x1801839ed
001836C2  f6058f284e033f                   test byte ptr [rip + 0x34e288f], 0x3f
001836C9  490fbfb8ee060000                 movsx rdi, word ptr [r8 + 0x6ee]
001836D1  7549                             jne 0x18018371c
001836D3  410fbf90e6060000                 movsx edx, word ptr [r8 + 0x6e6]
001836DB  488d0d2e38cf02                   lea rcx, [rip + 0x2cf382e]
001836E2  e859eef8ff                       call 0x180112540
001836E7  4869d73c580000                   imul rdx, rdi, 0x583c
001836EE  42018432b8e67903                 add dword ptr [rdx + r14 + 0x379e6b8], eax
001836F6  486315c7cb7a00                   movsxd rdx, dword ptr [rip + 0x7acbc7]
001836FD  4869ca90040000                   imul rcx, rdx, 0x490
00183704  4038ac19440a0000                 cmp byte ptr [rcx + rbx + 0xa44], bpl
0018370C  7f0e                             jg 0x18018371c
0018370E  488bcb                           mov rcx, rbx
00183711  e88ad20000                       call 0x1801909a0
00183716  8b15a8cb7a00                     mov edx, dword ptr [rip + 0x7acba8]
0018371C  4863c2                           movsxd rax, edx
0018371F  4869c890040000                   imul rcx, rax, 0x490
00183726  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
0018372E  6683f84a                         cmp ax, 0x4a
00183732  7513                             jne 0x180183747
00183734  486384192c070000                 movsxd rax, dword ptr [rcx + rbx + 0x72c]
0018373C  42c68430f09e440506               mov byte ptr [rax + r14 + 0x5449ef0], 6
00183745  eb1a                             jmp 0x180183761
00183747  6683f84c                         cmp ax, 0x4c
0018374B  7514                             jne 0x180183761
0018374D  41b806000000                     mov r8d, 6
00183753  488bcb                           mov rcx, rbx
00183756  e865c6ffff                       call 0x18017fdc0
0018375B  8b1563cb7a00                     mov edx, dword ptr [rip + 0x7acb63]
00183761  4c63c2                           movsxd r8, edx
00183764  4d69d890040000                   imul r11, r8, 0x490
0018376B  4c03db                           add r11, rbx
0018376E  410fb783e6060000                 movzx eax, word ptr [r11 + 0x6e6]
00183776  6683f837                         cmp ax, 0x37
0018377A  0f84c5010000                     je 0x180183945
00183780  490fbf8b820a0000                 movsx rcx, word ptr [r11 + 0xa82]
00183788  83f90a                           cmp ecx, 0xa
0018378B  751c                             jne 0x1801837a9
0018378D  4869cf3c580000                   imul rcx, rdi, 0x583c
00183794  42ff843178d97903                 inc dword ptr [rcx + r14 + 0x379d978]
0018379C  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
001837A4  e99c010000                       jmp 0x180183945
001837A9  6683f81e                         cmp ax, 0x1e
001837AD  0f8492010000                     je 0x180183945
001837B3  83f915                           cmp ecx, 0x15
001837B6  0f8789010000                     ja 0x180183945
001837BC  418b8c8eb44e1800                 mov ecx, dword ptr [r14 + rcx*4 + 0x184eb4]
001837C4  4903ce                           add rcx, r14
001837C7  ffe1                             jmp rcx
001837C9  4869cf3c580000                   imul rcx, rdi, 0x583c
001837D0  42ff8431b4de7903                 inc dword ptr [rcx + r14 + 0x379deb4]
001837D8  e968010000                       jmp 0x180183945
001837DD  4869cf3c580000                   imul rcx, rdi, 0x583c
001837E4  42ff8431b8de7903                 inc dword ptr [rcx + r14 + 0x379deb8]
001837EC  e954010000                       jmp 0x180183945
001837F1  4869cf3c580000                   imul rcx, rdi, 0x583c
001837F8  42ff8431bcde7903                 inc dword ptr [rcx + r14 + 0x379debc]
00183800  e940010000                       jmp 0x180183945
00183805  4869cf3c580000                   imul rcx, rdi, 0x583c
0018380C  42ff84310ce77903                 inc dword ptr [rcx + r14 + 0x379e70c]
00183814  e92c010000                       jmp 0x180183945
00183819  4869cf3c580000                   imul rcx, rdi, 0x583c
00183820  42ff843114e77903                 inc dword ptr [rcx + r14 + 0x379e714]
00183828  e918010000                       jmp 0x180183945
0018382D  4869cf3c580000                   imul rcx, rdi, 0x583c
00183834  42ff84317cd97903                 inc dword ptr [rcx + r14 + 0x379d97c]
0018383C  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
00183844  e9fc000000                       jmp 0x180183945
00183849  4869cf3c580000                   imul rcx, rdi, 0x583c
00183850  42ff843180d97903                 inc dword ptr [rcx + r14 + 0x379d980]
00183858  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
00183860  e9e0000000                       jmp 0x180183945
00183865  4869cf3c580000                   imul rcx, rdi, 0x583c
0018386C  42ff843184d97903                 inc dword ptr [rcx + r14 + 0x379d984]
00183874  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
0018387C  e9c4000000                       jmp 0x180183945
00183881  4869cf3c580000                   imul rcx, rdi, 0x583c
00183888  42ff843188d97903                 inc dword ptr [rcx + r14 + 0x379d988]
00183890  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
00183898  e9a8000000                       jmp 0x180183945
0018389D  4869cf3c580000                   imul rcx, rdi, 0x583c
001838A4  42ff84318cd97903                 inc dword ptr [rcx + r14 + 0x379d98c]
001838AC  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
001838B4  e98c000000                       jmp 0x180183945
001838B9  4869cf3c580000                   imul rcx, rdi, 0x583c
001838C0  42ff843190d97903                 inc dword ptr [rcx + r14 + 0x379d990]
001838C8  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
001838D0  eb73                             jmp 0x180183945
001838D2  4869cf3c580000                   imul rcx, rdi, 0x583c
001838D9  42ff843194d97903                 inc dword ptr [rcx + r14 + 0x379d994]
001838E1  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
001838E9  eb5a                             jmp 0x180183945
001838EB  4869cf3c580000                   imul rcx, rdi, 0x583c
001838F2  42ff843198d97903                 inc dword ptr [rcx + r14 + 0x379d998]
001838FA  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
00183902  eb41                             jmp 0x180183945
00183904  4869cf3c580000                   imul rcx, rdi, 0x583c
0018390B  42ff84319cd97903                 inc dword ptr [rcx + r14 + 0x379d99c]
00183913  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
0018391B  eb28                             jmp 0x180183945
0018391D  4869cf3c580000                   imul rcx, rdi, 0x583c
00183924  42ff8431a0d97903                 inc dword ptr [rcx + r14 + 0x379d9a0]
0018392C  42ff8431c0de7903                 inc dword ptr [rcx + r14 + 0x379dec0]
00183934  eb0f                             jmp 0x180183945
00183936  4869cf3c580000                   imul rcx, rdi, 0x583c
0018393D  42ff8431ece67903                 inc dword ptr [rcx + r14 + 0x379e6ec]
00183945  410fbf83e6060000                 movsx eax, word ptr [r11 + 0x6e6]
0018394D  83c0d9                           add eax, -0x27
00183950  83f826                           cmp eax, 0x26
00183953  0f8791000000                     ja 0x1801839ea
00183959  4898                             cdqe 
0018395B  410fb68406144f1800               movzx eax, byte ptr [r14 + rax + 0x184f14]
00183964  418b8c860c4f1800                 mov ecx, dword ptr [r14 + rax*4 + 0x184f0c]
0018396C  4903ce                           add rcx, r14
0018396F  ffe1                             jmp rcx
00183971  448bd5                           mov r10d, ebp
00183974  4c8bcd                           mov r9, rbp
00183977  66413bab0c0a0000                 cmp bp, word ptr [r11 + 0xa0c]
0018397F  7d69                             jge 0x1801839ea
00183981  4969c048020000                   imul rax, r8, 0x248
00183988  4903c1                           add rax, r9
0018398B  480fbf84436c090000               movsx rax, word ptr [rbx + rax*2 + 0x96c]
00183994  4869c890040000                   imul rcx, rax, 0x490
0018399B  4969c024010000                   imul rax, r8, 0x124
001839A2  4903c1                           add rax, r9
001839A5  8b848374090000                   mov eax, dword ptr [rbx + rax*4 + 0x974]
001839AC  398419f0060000                   cmp dword ptr [rcx + rbx + 0x6f0], eax
001839B3  7518                             jne 0x1801839cd
001839B5  6683bc191809000005               cmp word ptr [rcx + rbx + 0x918], 5
001839BE  750d                             jne 0x1801839cd
001839C0  89ac1974090000                   mov dword ptr [rcx + rbx + 0x974], ebp
001839C7  8b15f7c87a00                     mov edx, dword ptr [rip + 0x7ac8f7]
001839CD  4c63c2                           movsxd r8, edx
001839D0  41ffc2                           inc r10d
001839D3  4969c090040000                   imul rax, r8, 0x490
001839DA  49ffc1                           inc r9
001839DD  0fbf8c180c0a0000                 movsx ecx, word ptr [rax + rbx + 0xa0c]
001839E5  443bd1                           cmp r10d, ecx
001839E8  7c97                             jl 0x180183981
001839EA  418bfd                           mov edi, r13d
001839ED  4863c2                           movsxd rax, edx
001839F0  4c69c090040000                   imul r8, rax, 0x490
001839F7  4c03c3                           add r8, rbx
001839FA  410fb780e6060000                 movzx eax, word ptr [r8 + 0x6e6]
00183A02  6683e816                         sub ax, 0x16
00183A06  6683f83c                         cmp ax, 0x3c
00183A0A  774a                             ja 0x180183a56
00183A0C  480fa3c6                         bt rsi, rax
00183A10  7344                             jae 0x180183a56
00183A12  410fb78018090000                 movzx eax, word ptr [r8 + 0x918]
00183A1A  6683e805                         sub ax, 5
00183A1E  66413bc5                         cmp ax, r13w
00183A22  7732                             ja 0x180183a56
00183A24  490fbf809c090000                 movsx rax, word ptr [r8 + 0x99c]
00183A2C  6685c0                           test ax, ax
00183A2F  7e25                             jle 0x180183a56
00183A31  4869c890040000                   imul rcx, rax, 0x490
00183A38  418b80f8060000                   mov eax, dword ptr [r8 + 0x6f8]
00183A3F  398419f0060000                   cmp dword ptr [rcx + rbx + 0x6f0], eax
00183A46  750e                             jne 0x180183a56
00183A48  6689bc19f2090000                 mov word ptr [rcx + rbx + 0x9f2], di
00183A50  8b156ec87a00                     mov edx, dword ptr [rip + 0x7ac86e]
00183A56  4863c2                           movsxd rax, edx
00183A59  4869f890040000                   imul rdi, rax, 0x490
00183A60  4803fb                           add rdi, rbx
00183A63  8b8f940a0000                     mov ecx, dword ptr [rdi + 0xa94]
00183A69  85c9                             test ecx, ecx
00183A6B  7465                             je 0x180183ad2
00183A6D  8b87dc090000                     mov eax, dword ptr [rdi + 0x9dc]
00183A73  23c1                             and eax, ecx
00183A75  230d559a6603                     and ecx, dword ptr [rip + 0x3669a55]
00183A7B  3bc8                             cmp ecx, eax
00183A7D  7553                             jne 0x180183ad2
00183A7F  488bcb                           mov rcx, rbx
00183A82  6639afee060000                   cmp word ptr [rdi + 0x6ee], bp
00183A89  750e                             jne 0x180183a99
00183A8B  e8b0850000                       call 0x18018c040
00183A90  668987fe080000                   mov word ptr [rdi + 0x8fe], ax
00183A97  eb33                             jmp 0x180183acc
00183A99  e8c2860000                       call 0x18018c160
00183A9E  48630d1fc87a00                   movsxd rcx, dword ptr [rip + 0x7ac81f]
00183AA5  4869d190040000                   imul rdx, rcx, 0x490
00183AAC  6689841afe080000                 mov word ptr [rdx + rbx + 0x8fe], ax
00183AB4  48630509c87a00                   movsxd rax, dword ptr [rip + 0x7ac809]
00183ABB  4869c890040000                   imul rcx, rax, 0x490
00183AC2  8b431c                           mov eax, dword ptr [rbx + 0x1c]
00183AC5  898419900a0000                   mov dword ptr [rcx + rbx + 0xa90], eax
00183ACC  8b15f2c77a00                     mov edx, dword ptr [rip + 0x7ac7f2]
00183AD2  4863c2                           movsxd rax, edx
00183AD5  4869d090040000                   imul rdx, rax, 0x490
00183ADC  0fb7841a040a0000                 movzx eax, word ptr [rdx + rbx + 0xa04]
00183AE4  6685c0                           test ax, ax
00183AE7  8d4801                           lea ecx, [rax + 1]
00183AEA  660f49cd                         cmovns cx, bp
00183AEE  66898c1a040a0000                 mov word ptr [rdx + rbx + 0xa04], cx
00183AF6  486315c7c77a00                   movsxd rdx, dword ptr [rip + 0x7ac7c7]
00183AFD  4869ca90040000                   imul rcx, rdx, 0x490
00183B04  0fb784192a090000                 movzx eax, word ptr [rcx + rbx + 0x92a]
00183B0C  6685c0                           test ax, ax
00183B0F  7911                             jns 0x180183b22
00183B11  66ffc0                           inc ax
00183B14  668984192a090000                 mov word ptr [rcx + rbx + 0x92a], ax
00183B1C  8b15a2c77a00                     mov edx, dword ptr [rip + 0x7ac7a2]
00183B22  4863c2                           movsxd rax, edx
00183B25  4869c890040000                   imul rcx, rax, 0x490
00183B2C  6689ac19540a0000                 mov word ptr [rcx + rbx + 0xa54], bp
00183B34  392d0299f205                     cmp dword ptr [rip + 0x5f29902], ebp
00183B3A  0f84a7000000                     je 0x180183be7
00183B40  4863157dc77a00                   movsxd rdx, dword ptr [rip + 0x7ac77d]
00183B47  4c69c290040000                   imul r8, rdx, 0x490
00183B4E  410fb68418da060000               movzx eax, byte ptr [r8 + rbx + 0x6da]
00183B57  84c0                             test al, al
00183B59  0f848e000000                     je 0x180183bed
00183B5F  8b0d5bc77a00                     mov ecx, dword ptr [rip + 0x7ac75b]
00183B65  2bc8                             sub ecx, eax
00183B67  4863c1                           movsxd rax, ecx
00183B6A  410fb6848608693300               movzx eax, byte ptr [r14 + rax*4 + 0x336908]
00183B73  41288418db060000                 sub byte ptr [r8 + rbx + 0x6db], al
00183B7B  48631542c77a00                   movsxd rdx, dword ptr [rip + 0x7ac742]
00183B82  4869ca90040000                   imul rcx, rdx, 0x490
00183B89  80bc19db06000064                 cmp byte ptr [rcx + rbx + 0x6db], 0x64
00183B91  760e                             jbe 0x180183ba1
00183B93  4088ac19db060000                 mov byte ptr [rcx + rbx + 0x6db], bpl
00183B9B  8b1523c77a00                     mov edx, dword ptr [rip + 0x7ac723]
00183BA1  4863c2                           movsxd rax, edx
00183BA4  418bfd                           mov edi, r13d
00183BA7  4869c890040000                   imul rcx, rax, 0x490
00183BAE  0fb68419da060000                 movzx eax, byte ptr [rcx + rbx + 0x6da]
00183BB6  83c00d                           add eax, 0xd
00183BB9  390501c77a00                     cmp dword ptr [rip + 0x7ac701], eax
00183BBF  7c2f                             jl 0x180183bf0
00183BC1  4088ac19da060000                 mov byte ptr [rcx + rbx + 0x6da], bpl
00183BC9  486305f4c67a00                   movsxd rax, dword ptr [rip + 0x7ac6f4]
00183BD0  4869c890040000                   imul rcx, rax, 0x490
00183BD7  4088ac19db060000                 mov byte ptr [rcx + rbx + 0x6db], bpl
00183BDF  8b15dfc67a00                     mov edx, dword ptr [rip + 0x7ac6df]
00183BE5  eb09                             jmp 0x180183bf0
00183BE7  8b15d7c67a00                     mov edx, dword ptr [rip + 0x7ac6d7]
00183BED  418bfd                           mov edi, r13d
00183BF0  ffc2                             inc edx
00183BF2  8915ccc67a00                     mov dword ptr [rip + 0x7ac6cc], edx
00183BF8  3b13                             cmp edx, dword ptr [rbx]
00183BFA  0f8c10faffff                     jl 0x180183610
00183C00  488d0d796adf02                   lea rcx, [rip + 0x2df6a79]
00183C07  e8a48af1ff                       call 0x18009c6b0
00183C0C  488d0d6d6adf02                   lea rcx, [rip + 0x2df6a6d]
00183C13  e8f8bcf1ff                       call 0x18009f910
00183C18  48892da58e6608                   mov qword ptr [rip + 0x8668ea5], rbp
00183C1F  40882da68e6608                   mov byte ptr [rip + 0x8668ea6], bpl
00183C26  893d98c67a00                     mov dword ptr [rip + 0x7ac698], edi
00183C2C  44392b                           cmp dword ptr [rbx], r13d
00183C2F  0f8e49120000                     jle 0x180184e7e
00183C35  41bf02000000                     mov r15d, 2
00183C3B  4c8d258ec67a00                   lea r12, [rip + 0x7ac68e]
00183C42  458d4f6d                         lea r9d, [r15 + 0x6d]
00183C46  458d6f02                         lea r13d, [r15 + 2]
00183C4A  660f1f440000                     nop word ptr [rax + rax]
00183C50  4863056dc67a00                   movsxd rax, dword ptr [rip + 0x7ac66d]
00183C57  4869d090040000                   imul rdx, rax, 0x490
00183C5E  0fbf8c1ae4060000                 movsx ecx, word ptr [rdx + rbx + 0x6e4]
00183C66  85c9                             test ecx, ecx
00183C68  0f84fa110000                     je 0x180184e68
00183C6E  83e901                           sub ecx, 1
00183C71  7445                             je 0x180183cb8
00183C73  412bcf                           sub ecx, r15d
00183C76  742d                             je 0x180183ca5
00183C78  83e901                           sub ecx, 1
00183C7B  7412                             je 0x180183c8f
00183C7D  83f901                           cmp ecx, 1
00183C80  753f                             jne 0x180183cc1
00183C82  6689bc1ae4060000                 mov word ptr [rdx + rbx + 0x6e4], di
00183C8A  e9d9110000                       jmp 0x180184e68
00183C8F  8b152fc67a00                     mov edx, dword ptr [rip + 0x7ac62f]
00183C95  488bcb                           mov rcx, rbx
00183C98  e873200100                       call 0x180195d10
00183C9D  41b96f000000                     mov r9d, 0x6f
00183CA3  eb1c                             jmp 0x180183cc1
00183CA5  8b1519c67a00                     mov edx, dword ptr [rip + 0x7ac619]
00183CAB  488bcb                           mov rcx, rbx
00183CAE  e80d2d0000                       call 0x1801869c0
00183CB3  e9aa110000                       jmp 0x180184e62
00183CB8  664489bc1ae4060000               mov word ptr [rdx + rbx + 0x6e4], r15w
00183CC1  486305fcc57a00                   movsxd rax, dword ptr [rip + 0x7ac5fc]
00183CC8  4869c890040000                   imul rcx, rax, 0x490
00183CCF  ff841998060000                   inc dword ptr [rcx + rbx + 0x698]
00183CD6  486305e7c57a00                   movsxd rax, dword ptr [rip + 0x7ac5e7]
00183CDD  4c69c090040000                   imul r8, rax, 0x490
00183CE4  4c03c3                           add r8, rbx
00183CE7  418b809c060000                   mov eax, dword ptr [r8 + 0x69c]
00183CEE  41038094060000                   add eax, dword ptr [r8 + 0x694]
00183CF5  39841998060000                   cmp dword ptr [rcx + rbx + 0x698], eax
00183CFC  7e5d                             jle 0x180183d5b
00183CFE  418b8098060000                   mov eax, dword ptr [r8 + 0x698]
00183D05  41898094060000                   mov dword ptr [r8 + 0x694], eax
00183D0C  486305b1c57a00                   movsxd rax, dword ptr [rip + 0x7ac5b1]
00183D13  4869c890040000                   imul rcx, rax, 0x490
00183D1A  ff841908090000                   inc dword ptr [rcx + rbx + 0x908]
00183D21  4863059cc57a00                   movsxd rax, dword ptr [rip + 0x7ac59c]
00183D28  4869c890040000                   imul rcx, rax, 0x490
00183D2F  89bc19a8060000                   mov dword ptr [rcx + rbx + 0x6a8], edi
00183D36  48630d87c57a00                   movsxd rcx, dword ptr [rip + 0x7ac587]
00183D3D  4869d190040000                   imul rdx, rcx, 0x490
00183D44  81bc1a08090000e8030000           cmp dword ptr [rdx + rbx + 0x908], 0x3e8
00183D4F  7c17                             jl 0x180183d68
00183D51  4289ac32088d7e06                 mov dword ptr [rdx + r14 + 0x67e8d08], ebp
00183D59  eb0d                             jmp 0x180183d68
00183D5B  4189a8a8060000                   mov dword ptr [r8 + 0x6a8], ebp
00183D62  8b0d5cc57a00                     mov ecx, dword ptr [rip + 0x7ac55c]
00183D68  4863c1                           movsxd rax, ecx
00183D6B  4869c890040000                   imul rcx, rax, 0x490
00183D72  6639ac19b0060000                 cmp word ptr [rcx + rbx + 0x6b0], bp
00183D7A  7409                             je 0x180183d85
00183D7C  0fb7059996f205                   movzx eax, word ptr [rip + 0x5f29699]
00183D83  eb1d                             jmp 0x180183da2
00183D85  6683bc19180900006a               cmp word ptr [rcx + rbx + 0x918], 0x6a
00183D8E  750a                             jne 0x180183d9a
00183D90  0fb784190e090000                 movzx eax, word ptr [rcx + rbx + 0x90e]
00183D98  eb08                             jmp 0x180183da2
00183D9A  0fb784190c090000                 movzx eax, word ptr [rcx + rbx + 0x90c]
00183DA2  662b056396f205                   sub ax, word ptr [rip + 0x5f29663]
00183DA9  66898419ac060000                 mov word ptr [rcx + rbx + 0x6ac], ax
00183DB1  4863150cc57a00                   movsxd rdx, dword ptr [rip + 0x7ac50c]
00183DB8  4869ca90040000                   imul rcx, rdx, 0x490
00183DBF  0fb78419ac060000                 movzx eax, word ptr [rcx + rbx + 0x6ac]
00183DC7  6685c0                           test ax, ax
00183DCA  7912                             jns 0x180183dde
00183DCC  6683c008                         add ax, 8
00183DD0  66898419ac060000                 mov word ptr [rcx + rbx + 0x6ac], ax
00183DD8  8b15e6c47a00                     mov edx, dword ptr [rip + 0x7ac4e6]
00183DDE  4863c2                           movsxd rax, edx
00183DE1  4869c890040000                   imul rcx, rax, 0x490
00183DE8  480fbf8419ee060000               movsx rax, word ptr [rcx + rbx + 0x6ee]
00183DF1  4c69c03c580000                   imul r8, rax, 0x583c
00183DF8  43ff843054ae7903                 inc dword ptr [r8 + r14 + 0x379ae54]
00183E00  480fbf8419e6060000               movsx rax, word ptr [rcx + rbx + 0x6e6]
00183E09  4139ac86d0233200                 cmp dword ptr [r14 + rax*4 + 0x3223d0], ebp
00183E11  7408                             je 0x180183e1b
00183E13  43ff843048ae7903                 inc dword ptr [r8 + r14 + 0x379ae48]
00183E1B  892dabc47a00                     mov dword ptr [rip + 0x7ac4ab], ebp
00183E21  6639ac19f8080000                 cmp word ptr [rcx + rbx + 0x8f8], bp
00183E29  0f848a000000                     je 0x180183eb9
00183E2F  440fb7841918090000               movzx r8d, word ptr [rcx + rbx + 0x918]
00183E38  664183f86e                       cmp r8w, 0x6e
00183E3D  747a                             je 0x180183eb9
00183E3F  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
00183E47  66413bc7                         cmp ax, r15w
00183E4B  746c                             je 0x180183eb9
00183E4D  6683e844                         sub ax, 0x44
00183E51  6683f801                         cmp ax, 1
00183E55  7662                             jbe 0x180183eb9
00183E57  664183e86f                       sub r8w, 0x6f
00183E5C  664183f806                       cmp r8w, 6
00183E61  7656                             jbe 0x180183eb9
00183E63  8b841998060000                   mov eax, dword ptr [rcx + rbx + 0x698]
00183E6A  89841994060000                   mov dword ptr [rcx + rbx + 0x694], eax
00183E71  4863054cc47a00                   movsxd rax, dword ptr [rip + 0x7ac44c]
00183E78  4869c890040000                   imul rcx, rax, 0x490
00183E7F  4489bc199c060000                 mov dword ptr [rcx + rbx + 0x69c], r15d
00183E87  48630536c47a00                   movsxd rax, dword ptr [rip + 0x7ac436]
00183E8E  4869c890040000                   imul rcx, rax, 0x490
00183E95  6644898c1918090000               mov word ptr [rcx + rbx + 0x918], r9w
00183E9E  4863051fc47a00                   movsxd rax, dword ptr [rip + 0x7ac41f]
00183EA5  4869c890040000                   imul rcx, rax, 0x490
00183EAC  89ac1908090000                   mov dword ptr [rcx + rbx + 0x908], ebp
00183EB3  8b150bc47a00                     mov edx, dword ptr [rip + 0x7ac40b]
00183EB9  4863c2                           movsxd rax, edx
00183EBC  4869c890040000                   imul rcx, rax, 0x490
00183EC3  480fbf8419e6060000               movsx rax, word ptr [rcx + rbx + 0x6e6]
00183ECC  4139ac8660223200                 cmp dword ptr [r14 + rax*4 + 0x322260], ebp
00183ED4  0f844e010000                     je 0x180184028
00183EDA  4a0fbf8431908d7e06               movsx rax, word ptr [rcx + r14 + 0x67e8d90]
00183EE3  4c69c02c030000                   imul r8, rax, 0x32c
00183EEA  8b8419c0090000                   mov eax, dword ptr [rcx + rbx + 0x9c0]
00183EF1  43398430e4cc4c06                 cmp dword ptr [r8 + r14 + 0x64ccce4], eax
00183EF9  0f8483000000                     je 0x180183f82
00183EFF  488bcb                           mov rcx, rbx
00183F02  e8493a0100                       call 0x180197950
00183F07  486315b6c37a00                   movsxd rdx, dword ptr [rip + 0x7ac3b6]
00183F0E  4869ca90040000                   imul rcx, rdx, 0x490
00183F15  6683bc195007000008               cmp word ptr [rcx + rbx + 0x750], 8
00183F1E  0f8c04010000                     jl 0x180184028
00183F24  664489ac19e4060000               mov word ptr [rcx + rbx + 0x6e4], r13w
00183F2D  48630590c37a00                   movsxd rax, dword ptr [rip + 0x7ac390]
00183F34  4869c890040000                   imul rcx, rax, 0x490
00183F3B  6689bc1922090000                 mov word ptr [rcx + rbx + 0x922], di
00183F43  4863057ac37a00                   movsxd rax, dword ptr [rip + 0x7ac37a]
00183F4A  4869c890040000                   imul rcx, rax, 0x490
00183F51  6689ac1924090000                 mov word ptr [rcx + rbx + 0x924], bp
00183F59  48630564c37a00                   movsxd rax, dword ptr [rip + 0x7ac364]
00183F60  4869c890040000                   imul rcx, rax, 0x490
00183F67  6689ac1990090000                 mov word ptr [rcx + rbx + 0x990], bp
00183F6F  488bcb                           mov rcx, rbx
00183F72  8b154cc37a00                     mov edx, dword ptr [rip + 0x7ac34c]
00183F78  e883320000                       call 0x180187200
00183F7D  e9a0000000                       jmp 0x180184022
00183F82  4338ac30a2ce4c06                 cmp byte ptr [r8 + r14 + 0x64ccea2], bpl
00183F8A  0f8498000000                     je 0x180184028
00183F90  488bcb                           mov rcx, rbx
00183F93  e8b8390100                       call 0x180197950
00183F98  48631525c37a00                   movsxd rdx, dword ptr [rip + 0x7ac325]
00183F9F  4869ca90040000                   imul rcx, rdx, 0x490
00183FA6  6683bc195007000008               cmp word ptr [rcx + rbx + 0x750], 8
00183FAF  7c5f                             jl 0x180184010
00183FB1  664489ac19e4060000               mov word ptr [rcx + rbx + 0x6e4], r13w
00183FBA  48630503c37a00                   movsxd rax, dword ptr [rip + 0x7ac303]
00183FC1  4869c890040000                   imul rcx, rax, 0x490
00183FC8  6689bc1922090000                 mov word ptr [rcx + rbx + 0x922], di
00183FD0  486305edc27a00                   movsxd rax, dword ptr [rip + 0x7ac2ed]
00183FD7  4869c890040000                   imul rcx, rax, 0x490
00183FDE  6689ac1924090000                 mov word ptr [rcx + rbx + 0x924], bp
00183FE6  486305d7c27a00                   movsxd rax, dword ptr [rip + 0x7ac2d7]
00183FED  4869c890040000                   imul rcx, rax, 0x490
00183FF4  6689ac1990090000                 mov word ptr [rcx + rbx + 0x990], bp
00183FFC  488bcb                           mov rcx, rbx
00183FFF  8b15bfc27a00                     mov edx, dword ptr [rip + 0x7ac2bf]
00184005  e8f6310000                       call 0x180187200
0018400A  8b15b4c27a00                     mov edx, dword ptr [rip + 0x7ac2b4]
00184010  4863c2                           movsxd rax, edx
00184013  4869c890040000                   imul rcx, rax, 0x490
0018401A  4088ac1986090000                 mov byte ptr [rcx + rbx + 0x986], bpl
00184022  8b159cc27a00                     mov edx, dword ptr [rip + 0x7ac29c]
00184028  4863c2                           movsxd rax, edx
0018402B  4869c890040000                   imul rcx, rax, 0x490
00184032  6689ac1904090000                 mov word ptr [rcx + rbx + 0x904], bp
0018403A  488bcb                           mov rcx, rbx
0018403D  8b1581c27a00                     mov edx, dword ptr [rip + 0x7ac281]
00184043  e828110100                       call 0x180195170
00184048  48631575c27a00                   movsxd rdx, dword ptr [rip + 0x7ac275]
0018404F  4869ca90040000                   imul rcx, rdx, 0x490
00184056  6639ac19b0090000                 cmp word ptr [rcx + rbx + 0x9b0], bp
0018405E  7452                             je 0x1801840b2
00184060  6639ac19fc080000                 cmp word ptr [rcx + rbx + 0x8fc], bp
00184068  7448                             je 0x1801840b2
0018406A  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
00184072  6683f81e                         cmp ax, 0x1e
00184076  743a                             je 0x1801840b2
00184078  6683f805                         cmp ax, 5
0018407C  7518                             jne 0x180184096
0018407E  0fb7841918090000                 movzx eax, word ptr [rcx + rbx + 0x918]
00184086  6683f809                         cmp ax, 9
0018408A  7426                             je 0x1801840b2
0018408C  6683e803                         sub ax, 3
00184090  6683f801                         cmp ax, 1
00184094  761c                             jbe 0x1801840b2
00184096  6689ac19b0090000                 mov word ptr [rcx + rbx + 0x9b0], bp
0018409E  488bcb                           mov rcx, rbx
001840A1  8b151dc27a00                     mov edx, dword ptr [rip + 0x7ac21d]
001840A7  e884c1ffff                       call 0x180180230
001840AC  8b1512c27a00                     mov edx, dword ptr [rip + 0x7ac212]
001840B2  4863c2                           movsxd rax, edx
001840B5  4869c890040000                   imul rcx, rax, 0x490
001840BC  0fb784191c090000                 movzx eax, word ptr [rcx + rbx + 0x91c]
001840C4  66898419bc0a0000                 mov word ptr [rcx + rbx + 0xabc], ax
001840CC  486305f1c17a00                   movsxd rax, dword ptr [rip + 0x7ac1f1]
001840D3  4869c890040000                   imul rcx, rax, 0x490
001840DA  0fb7841918090000                 movzx eax, word ptr [rcx + rbx + 0x918]
001840E2  668984191c090000                 mov word ptr [rcx + rbx + 0x91c], ax
001840EA  486315d3c17a00                   movsxd rdx, dword ptr [rip + 0x7ac1d3]
001840F1  4869ca90040000                   imul rcx, rdx, 0x490
001840F8  664439ac19e4060000               cmp word ptr [rcx + rbx + 0x6e4], r13w
00184101  7417                             je 0x18018411a
00184103  480fbf8419e6060000               movsx rax, word ptr [rcx + rbx + 0x6e6]
0018410C  41ff94c6b01c3200                 call qword ptr [r14 + rax*8 + 0x321cb0]
00184114  8b15aac17a00                     mov edx, dword ptr [rip + 0x7ac1aa]
0018411A  4863c2                           movsxd rax, edx
0018411D  4869c890040000                   imul rcx, rax, 0x490
00184124  6683bc19e606000037               cmp word ptr [rcx + rbx + 0x6e6], 0x37
0018412D  752b                             jne 0x18018415a
0018412F  664439bc19e4060000               cmp word ptr [rcx + rbx + 0x6e4], r15w
00184138  7520                             jne 0x18018415a
0018413A  6639ac19f8080000                 cmp word ptr [rcx + rbx + 0x8f8], bp
00184142  7516                             jne 0x18018415a
00184144  480fbf8419ee060000               movsx rax, word ptr [rcx + rbx + 0x6ee]
0018414D  89bc8314060000                   mov dword ptr [rbx + rax*4 + 0x614], edi
00184154  8b156ac17a00                     mov edx, dword ptr [rip + 0x7ac16a]
0018415A  4863c2                           movsxd rax, edx
0018415D  4869c890040000                   imul rcx, rax, 0x490
00184164  0fb784191c090000                 movzx eax, word ptr [rcx + rbx + 0x91c]
0018416C  663b841918090000                 cmp ax, word ptr [rcx + rbx + 0x918]
00184174  743c                             je 0x1801841b2
00184176  6683e87c                         sub ax, 0x7c
0018417A  6683f801                         cmp ax, 1
0018417E  7732                             ja 0x1801841b2
00184180  48638419100a0000                 movsxd rax, dword ptr [rcx + rbx + 0xa10]
00184188  48c1e004                         shl rax, 4
0018418C  4138ac06ecdbf905                 cmp byte ptr [r14 + rax + 0x5f9dbec], bpl
00184194  741c                             je 0x1801841b2
00184196  488d90efdbf905                   lea rdx, [rax + 0x5f9dbef]
0018419D  420fb60432                       movzx eax, byte ptr [rdx + r14]
001841A2  3c14                             cmp al, 0x14
001841A4  7c08                             jl 0x1801841ae
001841A6  2c14                             sub al, 0x14
001841A8  42880432                         mov byte ptr [rdx + r14], al
001841AC  eb04                             jmp 0x1801841b2
001841AE  42882c32                         mov byte ptr [rdx + r14], bpl
001841B2  89ac19b4060000                   mov dword ptr [rcx + rbx + 0x6b4], ebp
001841B9  48631504c17a00                   movsxd rdx, dword ptr [rip + 0x7ac104]
001841C0  4869c290040000                   imul rax, rdx, 0x490
001841C7  4803c3                           add rax, rbx
001841CA  6639a8f6080000                   cmp word ptr [rax + 0x8f6], bp
001841D1  7413                             je 0x1801841e6
001841D3  488bcb                           mov rcx, rbx
001841D6  e835690100                       call 0x18019ab10
001841DB  8b15e3c07a00                     mov edx, dword ptr [rip + 0x7ac0e3]
001841E1  e9ca070000                       jmp 0x1801849b0
001841E6  ff80ac090000                     inc dword ptr [rax + 0x9ac]
001841EC  8b90ac090000                     mov edx, dword ptr [rax + 0x9ac]
001841F2  4c630dcbc07a00                   movsxd r9, dword ptr [rip + 0x7ac0cb]
001841F9  4d69c190040000                   imul r8, r9, 0x490
00184200  4c03c3                           add r8, rbx
00184203  410fbf8016090000                 movsx eax, word ptr [r8 + 0x916]
0018420B  410fbf88a2090000                 movsx ecx, word ptr [r8 + 0x9a2]
00184213  458b90a8090000                   mov r10d, dword ptr [r8 + 0x9a8]
0018421A  03c8                             add ecx, eax
0018421C  410fbf804c070000                 movsx eax, word ptr [r8 + 0x74c]
00184224  412bd2                           sub edx, r10d
00184227  03c1                             add eax, ecx
00184229  418b88ac090000                   mov ecx, dword ptr [r8 + 0x9ac]
00184230  3bd0                             cmp edx, eax
00184232  0f8e5a070000                     jle 0x180184992
00184238  418988a8090000                   mov dword ptr [r8 + 0x9a8], ecx
0018423F  4863057ec07a00                   movsxd rax, dword ptr [rip + 0x7ac07e]
00184246  4869d090040000                   imul rdx, rax, 0x490
0018424D  0fb7841a14090000                 movzx eax, word ptr [rdx + rbx + 0x914]
00184255  6685c0                           test ax, ax
00184258  8d48ff                           lea ecx, [rax - 1]
0018425B  660f4ecd                         cmovle cx, bp
0018425F  4533c9                           xor r9d, r9d
00184262  66898c1a14090000                 mov word ptr [rdx + rbx + 0x914], cx
0018426A  48631553c07a00                   movsxd rdx, dword ptr [rip + 0x7ac053]
00184271  4869ca90040000                   imul rcx, rdx, 0x490
00184278  440fbf841916090000               movsx r8d, word ptr [rcx + rbx + 0x916]
00184281  488bcb                           mov rcx, rbx
00184284  e817130000                       call 0x1801855a0
00184289  85c0                             test eax, eax
0018428B  0f84dd060000                     je 0x18018496e
00184291  4863152cc07a00                   movsxd rdx, dword ptr [rip + 0x7ac02c]
00184298  4869ca90040000                   imul rcx, rdx, 0x490
0018429F  6683bc191809000065               cmp word ptr [rcx + rbx + 0x918], 0x65
001842A8  7522                             jne 0x1801842cc
001842AA  4038ac1986090000                 cmp byte ptr [rcx + rbx + 0x986], bpl
001842B2  7418                             je 0x1801842cc
001842B4  4038ac19530a0000                 cmp byte ptr [rcx + rbx + 0xa53], bpl
001842BC  750e                             jne 0x1801842cc
001842BE  4088ac1986090000                 mov byte ptr [rcx + rbx + 0x986], bpl
001842C6  8b15f8bf7a00                     mov edx, dword ptr [rip + 0x7abff8]
001842CC  4863c2                           movsxd rax, edx
001842CF  4869c890040000                   imul rcx, rax, 0x490
001842D6  66ff8419d6090000                 inc word ptr [rcx + rbx + 0x9d6]
001842DE  486315dfbf7a00                   movsxd rdx, dword ptr [rip + 0x7abfdf]
001842E5  4869ca90040000                   imul rcx, rdx, 0x490
001842EC  6683bc19d609000010               cmp word ptr [rcx + rbx + 0x9d6], 0x10
001842F5  7c0e                             jl 0x180184305
001842F7  6689ac19d6090000                 mov word ptr [rcx + rbx + 0x9d6], bp
001842FF  8b15bfbf7a00                     mov edx, dword ptr [rip + 0x7abfbf]
00184305  4863c2                           movsxd rax, edx
00184308  4869c890040000                   imul rcx, rax, 0x490
0018430F  664401bc19d4090000               add word ptr [rcx + rbx + 0x9d4], r15w
00184318  486315a5bf7a00                   movsxd rdx, dword ptr [rip + 0x7abfa5]
0018431F  4869ca90040000                   imul rcx, rdx, 0x490
00184326  6683bc19d40900000e               cmp word ptr [rcx + rbx + 0x9d4], 0xe
0018432F  7c0e                             jl 0x18018433f
00184331  6689ac19d4090000                 mov word ptr [rcx + rbx + 0x9d4], bp
00184339  8b1585bf7a00                     mov edx, dword ptr [rip + 0x7abf85]
0018433F  4863c2                           movsxd rax, edx
00184342  4869c890040000                   imul rcx, rax, 0x490
00184349  664401bc19d0090000               add word ptr [rcx + rbx + 0x9d0], r15w
00184352  4863156bbf7a00                   movsxd rdx, dword ptr [rip + 0x7abf6b]
00184359  4869ca90040000                   imul rcx, rdx, 0x490
00184360  6683bc19d009000010               cmp word ptr [rcx + rbx + 0x9d0], 0x10
00184369  7c0e                             jl 0x180184379
0018436B  6689ac19d0090000                 mov word ptr [rcx + rbx + 0x9d0], bp
00184373  8b154bbf7a00                     mov edx, dword ptr [rip + 0x7abf4b]
00184379  4863c2                           movsxd rax, edx
0018437C  4869c890040000                   imul rcx, rax, 0x490
00184383  6689ac1974060000                 mov word ptr [rcx + rbx + 0x674], bp
0018438B  48630532bf7a00                   movsxd rax, dword ptr [rip + 0x7abf32]
00184392  4869c890040000                   imul rcx, rax, 0x490
00184399  66ff84191e0a0000                 inc word ptr [rcx + rbx + 0xa1e]
001843A1  6683bc191e0a000012               cmp word ptr [rcx + rbx + 0xa1e], 0x12
001843AA  7c16                             jl 0x1801843c2
001843AC  48630511bf7a00                   movsxd rax, dword ptr [rip + 0x7abf11]
001843B3  4869c890040000                   imul rcx, rax, 0x490
001843BA  6689ac191e0a0000                 mov word ptr [rcx + rbx + 0xa1e], bp
001843C2  486305fbbe7a00                   movsxd rax, dword ptr [rip + 0x7abefb]
001843C9  4869c890040000                   imul rcx, rax, 0x490
001843D0  66ff8419800a0000                 inc word ptr [rcx + rbx + 0xa80]
001843D8  6683bc19800a000018               cmp word ptr [rcx + rbx + 0xa80], 0x18
001843E1  7c16                             jl 0x1801843f9
001843E3  486305dabe7a00                   movsxd rax, dword ptr [rip + 0x7abeda]
001843EA  4869c890040000                   imul rcx, rax, 0x490
001843F1  6689ac19800a0000                 mov word ptr [rcx + rbx + 0xa80], bp
001843F9  8b15c5be7a00                     mov edx, dword ptr [rip + 0x7abec5]
001843FF  4533c0                           xor r8d, r8d
00184402  488bcb                           mov rcx, rbx
00184405  e8566e0100                       call 0x18019b260
0018440A  8b15b4be7a00                     mov edx, dword ptr [rip + 0x7abeb4]
00184410  488bcb                           mov rcx, rbx
00184413  e8b80b0000                       call 0x180184fd0
00184418  4c6315a5be7a00                   movsxd r10, dword ptr [rip + 0x7abea5]
0018441F  4d69ca90040000                   imul r9, r10, 0x490
00184426  4c03cb                           add r9, rbx
00184429  410fbe89c8060000                 movsx ecx, byte ptr [r9 + 0x6c8]
00184431  83f901                           cmp ecx, 1
00184434  7e14                             jle 0x18018444a
00184436  664183b9e606000058               cmp word ptr [r9 + 0x6e6], 0x58
0018443F  7509                             jne 0x18018444a
00184441  8bc1                             mov eax, ecx
00184443  99                               cdq 
00184444  2bc2                             sub eax, edx
00184446  d1f8                             sar eax, 1
00184448  8bc8                             mov ecx, eax
0018444A  410fbf8114070000                 movsx eax, word ptr [r9 + 0x714]
00184452  41b809000000                     mov r8d, 9
00184458  410fbfb9dc060000                 movsx edi, word ptr [r9 + 0x6dc]
00184460  2bf8                             sub edi, eax
00184462  410fbf8112070000                 movsx eax, word ptr [r9 + 0x712]
0018446A  2bf8                             sub edi, eax
0018446C  410fbf8184060000                 movsx eax, word ptr [r9 + 0x684]
00184474  2bf8                             sub edi, eax
00184476  498d8158060000                   lea rax, [r9 + 0x658]
0018447D  03f9                             add edi, ecx
0018447F  498bcc                           mov rcx, r12
00184482  488d8980000000                   lea rcx, [rcx + 0x80]
00184489  0f1000                           movups xmm0, xmmword ptr [rax]
0018448C  488d8080000000                   lea rax, [rax + 0x80]
00184493  0f114180                         movups xmmword ptr [rcx - 0x80], xmm0
00184497  0f104890                         movups xmm1, xmmword ptr [rax - 0x70]
0018449B  0f114990                         movups xmmword ptr [rcx - 0x70], xmm1
0018449F  0f1040a0                         movups xmm0, xmmword ptr [rax - 0x60]
001844A3  0f1141a0                         movups xmmword ptr [rcx - 0x60], xmm0
001844A7  0f1048b0                         movups xmm1, xmmword ptr [rax - 0x50]
001844AB  0f1149b0                         movups xmmword ptr [rcx - 0x50], xmm1
001844AF  0f1040c0                         movups xmm0, xmmword ptr [rax - 0x40]
001844B3  0f1141c0                         movups xmmword ptr [rcx - 0x40], xmm0
001844B7  0f1048d0                         movups xmm1, xmmword ptr [rax - 0x30]
001844BB  0f1149d0                         movups xmmword ptr [rcx - 0x30], xmm1
001844BF  0f1040e0                         movups xmm0, xmmword ptr [rax - 0x20]
001844C3  0f1141e0                         movups xmmword ptr [rcx - 0x20], xmm0
001844C7  0f1048f0                         movups xmm1, xmmword ptr [rax - 0x10]
001844CB  0f1149f0                         movups xmmword ptr [rcx - 0x10], xmm1
001844CF  4983e801                         sub r8, 1
001844D3  75ad                             jne 0x180184482
001844D5  0f1000                           movups xmm0, xmmword ptr [rax]
001844D8  418bd2                           mov edx, r10d
001844DB  0f1101                           movups xmmword ptr [rcx], xmm0
001844DE  450fbf8116090000                 movsx r8d, word ptr [r9 + 0x916]
001844E6  488bcb                           mov rcx, rbx
001844E9  41b101                           mov r9b, 1
001844EC  e8af100000                       call 0x1801855a0
001844F1  85c0                             test eax, eax
001844F3  0f84f0030000                     je 0x1801848e9
001844F9  486315c4bd7a00                   movsxd rdx, dword ptr [rip + 0x7abdc4]
00184500  4869ca90040000                   imul rcx, rdx, 0x490
00184507  39ac1940070000                   cmp dword ptr [rcx + rbx + 0x740], ebp
0018450E  7414                             je 0x180184524
00184510  8b84193c070000                   mov eax, dword ptr [rcx + rbx + 0x73c]
00184517  8984192c070000                   mov dword ptr [rcx + rbx + 0x72c], eax
0018451E  8b15a0bd7a00                     mov edx, dword ptr [rip + 0x7abda0]
00184524  41b001                           mov r8b, 1
00184527  488bcb                           mov rcx, rbx
0018452A  e8316d0100                       call 0x18019b260
0018452F  8b158fbd7a00                     mov edx, dword ptr [rip + 0x7abd8f]
00184535  488bcb                           mov rcx, rbx
00184538  e8930a0000                       call 0x180184fd0
0018453D  48630580bd7a00                   movsxd rax, dword ptr [rip + 0x7abd80]
00184544  488d0cc0                         lea rcx, [rax + rax*8]
00184548  c6844b583fb20001                 mov byte ptr [rbx + rcx*2 + 0xb23f58], 1
00184550  4863056dbd7a00                   movsxd rax, dword ptr [rip + 0x7abd6d]
00184557  488d0cc0                         lea rcx, [rax + rax*8]
0018455B  6689ac4b643fb200                 mov word ptr [rbx + rcx*2 + 0xb23f64], bp
00184563  4863155abd7a00                   movsxd rdx, dword ptr [rip + 0x7abd5a]
0018456A  4869c290040000                   imul rax, rdx, 0x490
00184571  0fb78c18a2090000                 movzx ecx, word ptr [rax + rbx + 0x9a2]
00184579  66038c1816090000                 add cx, word ptr [rax + rbx + 0x916]
00184581  66038c184c070000                 add cx, word ptr [rax + rbx + 0x74c]
00184589  488d04d2                         lea rax, [rdx + rdx*8]
0018458D  66898c43663fb200                 mov word ptr [rbx + rax*2 + 0xb23f66], cx
00184595  48630d28bd7a00                   movsxd rcx, dword ptr [rip + 0x7abd28]
0018459C  4869d190040000                   imul rdx, rcx, 0x490
001845A3  488d04c9                         lea rax, [rcx + rcx*8]
001845A7  488d0c43                         lea rcx, [rbx + rax*2]
001845AB  39ac1a40070000                   cmp dword ptr [rdx + rbx + 0x740], ebp
001845B2  742b                             je 0x1801845df
001845B4  0fb7841a38070000                 movzx eax, word ptr [rdx + rbx + 0x738]
001845BC  6689815a3fb200                   mov word ptr [rcx + 0xb23f5a], ax
001845C3  486305fabc7a00                   movsxd rax, dword ptr [rip + 0x7abcfa]
001845CA  4869c890040000                   imul rcx, rax, 0x490
001845D1  488d14c0                         lea rdx, [rax + rax*8]
001845D5  0fb784193a070000                 movzx eax, word ptr [rcx + rbx + 0x73a]
001845DD  eb29                             jmp 0x180184608
001845DF  0fb7841a1c070000                 movzx eax, word ptr [rdx + rbx + 0x71c]
001845E7  6689815a3fb200                   mov word ptr [rcx + 0xb23f5a], ax
001845EE  486305cfbc7a00                   movsxd rax, dword ptr [rip + 0x7abccf]
001845F5  4869c890040000                   imul rcx, rax, 0x490
001845FC  488d14c0                         lea rdx, [rax + rax*8]
00184600  0fb784191e070000                 movzx eax, word ptr [rcx + rbx + 0x71e]
00184608  668984535c3fb200                 mov word ptr [rbx + rdx*2 + 0xb23f5c], ax
00184610  486305adbc7a00                   movsxd rax, dword ptr [rip + 0x7abcad]
00184617  4869c890040000                   imul rcx, rax, 0x490
0018461E  488d14c0                         lea rdx, [rax + rax*8]
00184622  0fb784197e060000                 movzx eax, word ptr [rcx + rbx + 0x67e]
0018462A  668984535e3fb200                 mov word ptr [rbx + rdx*2 + 0xb23f5e], ax
00184632  4863058bbc7a00                   movsxd rax, dword ptr [rip + 0x7abc8b]
00184639  4869c890040000                   imul rcx, rax, 0x490
00184640  488d14c0                         lea rdx, [rax + rax*8]
00184644  0fb7841980060000                 movzx eax, word ptr [rcx + rbx + 0x680]
0018464C  66898453603fb200                 mov word ptr [rbx + rdx*2 + 0xb23f60], ax
00184654  48633569bc7a00                   movsxd rsi, dword ptr [rip + 0x7abc69]
0018465B  4c69d690040000                   imul r10, rsi, 0x490
00184662  4c03d3                           add r10, rbx
00184665  450fbe8ac8060000                 movsx r9d, byte ptr [r10 + 0x6c8]
0018466D  4183f901                         cmp r9d, 1
00184671  7e16                             jle 0x180184689
00184673  664183bae606000058               cmp word ptr [r10 + 0x6e6], 0x58
0018467C  750b                             jne 0x180184689
0018467E  418bc1                           mov eax, r9d
00184681  99                               cdq 
00184682  2bc2                             sub eax, edx
00184684  d1f8                             sar eax, 1
00184686  448bc8                           mov r9d, eax
00184689  450fb79adc060000                 movzx r11d, word ptr [r10 + 0x6dc]
00184691  488d04f6                         lea rax, [rsi + rsi*8]
00184695  66452b9a14070000                 sub r11w, word ptr [r10 + 0x714]
0018469D  488d1443                         lea rdx, [rbx + rax*2]
001846A1  480fbf825c3fb200                 movsx rax, word ptr [rdx + 0xb23f5c]
001846A9  66452b9a84060000                 sub r11w, word ptr [r10 + 0x684]
001846B1  664503d9                         add r11w, r9w
001846B5  488d0c40                         lea rcx, [rax + rax*2]
001846B9  0fbf825a3fb200                   movsx eax, word ptr [rdx + 0xb23f5a]
001846C0  418b8c8e2cff0204                 mov ecx, dword ptr [r14 + rcx*4 + 0x402ff2c]
001846C8  03c8                             add ecx, eax
001846CA  4863d1                           movsxd rdx, ecx
001846CD  420fb6843250d3dd04               movzx eax, byte ptr [rdx + r14 + 0x4ddd350]
001846D6  450fb78486305e0604               movzx r8d, word ptr [r14 + rax*4 + 0x4065e30]
001846DF  418b82bc060000                   mov eax, dword ptr [r10 + 0x6bc]
001846E6  83f8ff                           cmp eax, -1
001846E9  7505                             jne 0x1801846f0
001846EB  0fb7cd                           movzx ecx, bp
001846EE  eb64                             jmp 0x180184754
001846F0  83f801                           cmp eax, 1
001846F3  7505                             jne 0x1801846fa
001846F5  418bcd                           mov ecx, r13d
001846F8  eb5a                             jmp 0x180184754
001846FA  413bc7                           cmp eax, r15d
001846FD  7507                             jne 0x180184706
001846FF  b905000000                       mov ecx, 5
00184704  eb4e                             jmp 0x180184754
00184706  83f80a                           cmp eax, 0xa
00184709  7505                             jne 0x180184710
0018470B  8d48f7                           lea ecx, [rax - 9]
0018470E  eb44                             jmp 0x180184754
00184710  83f80b                           cmp eax, 0xb
00184713  7505                             jne 0x18018471a
00184715  8d48f4                           lea ecx, [rax - 0xc]
00184718  eb3a                             jmp 0x180184754
0018471A  83f80c                           cmp eax, 0xc
0018471D  7505                             jne 0x180184724
0018471F  8d48f2                           lea ecx, [rax - 0xe]
00184722  eb30                             jmp 0x180184754
00184724  420fbe8432a0acd205               movsx eax, byte ptr [rdx + r14 + 0x5d2aca0]
0018472D  4585c9                           test r9d, r9d
00184730  740b                             je 0x18018473d
00184732  3c03                             cmp al, 3
00184734  7e1b                             jle 0x180184751
00184736  b803000000                       mov eax, 3
0018473B  eb14                             jmp 0x180184751
0018473D  41f68456f04cf60410               test byte ptr [r14 + rdx*2 + 0x4f64cf0], 0x10
00184746  7509                             jne 0x180184751
00184748  413ac7                           cmp al, r15b
0018474B  7e04                             jle 0x180184751
0018474D  410fb7c7                         movzx eax, r15w
00184751  0fb7c8                           movzx ecx, ax
00184754  488d04f6                         lea rax, [rsi + rsi*8]
00184758  66898c43683fb200                 mov word ptr [rbx + rax*2 + 0xb23f68], cx
00184760  392dd68cf205                     cmp dword ptr [rip + 0x5f28cd6], ebp
00184766  48631557bb7a00                   movsxd rdx, dword ptr [rip + 0x7abb57]
0018476D  743b                             je 0x1801847aa
0018476F  392dcf8cf205                     cmp dword ptr [rip + 0x5f28ccf], ebp
00184775  741a                             je 0x180184791
00184777  392dcf8cf205                     cmp dword ptr [rip + 0x5f28ccf], ebp
0018477D  7412                             je 0x180184791
0018477F  4869ca90040000                   imul rcx, rdx, 0x490
00184786  66442b841914070000               sub r8w, word ptr [rcx + rbx + 0x714]
0018478F  eb19                             jmp 0x1801847aa
00184791  4869ca90040000                   imul rcx, rdx, 0x490
00184798  440fb68419db060000               movzx r8d, byte ptr [rcx + rbx + 0x6db]
001847A1  664403841912070000               add r8w, word ptr [rcx + rbx + 0x712]
001847AA  4869c290040000                   imul rax, rdx, 0x490
001847B1  498bcc                           mov rcx, r12
001847B4  ba09000000                       mov edx, 9
001847B9  480558060000                     add rax, 0x658
001847BF  4803c3                           add rax, rbx
001847C2  488d8080000000                   lea rax, [rax + 0x80]
001847C9  0f1001                           movups xmm0, xmmword ptr [rcx]
001847CC  488d8980000000                   lea rcx, [rcx + 0x80]
001847D3  0f114080                         movups xmmword ptr [rax - 0x80], xmm0
001847D7  0f104990                         movups xmm1, xmmword ptr [rcx - 0x70]
001847DB  0f114890                         movups xmmword ptr [rax - 0x70], xmm1
001847DF  0f1041a0                         movups xmm0, xmmword ptr [rcx - 0x60]
001847E3  0f1140a0                         movups xmmword ptr [rax - 0x60], xmm0
001847E7  0f1049b0                         movups xmm1, xmmword ptr [rcx - 0x50]
001847EB  0f1148b0                         movups xmmword ptr [rax - 0x50], xmm1
001847EF  0f1041c0                         movups xmm0, xmmword ptr [rcx - 0x40]
001847F3  0f1140c0                         movups xmmword ptr [rax - 0x40], xmm0
001847F7  0f1049d0                         movups xmm1, xmmword ptr [rcx - 0x30]
001847FB  0f1148d0                         movups xmmword ptr [rax - 0x30], xmm1
001847FF  0f1041e0                         movups xmm0, xmmword ptr [rcx - 0x20]
00184803  0f1140e0                         movups xmmword ptr [rax - 0x20], xmm0
00184807  0f1049f0                         movups xmm1, xmmword ptr [rcx - 0x10]
0018480B  0f1148f0                         movups xmmword ptr [rax - 0x10], xmm1
0018480F  4883ea01                         sub rdx, 1
00184813  75ad                             jne 0x1801847c2
00184815  0f1001                           movups xmm0, xmmword ptr [rcx]
00184818  0f1100                           movups xmmword ptr [rax], xmm0
0018481B  48630da2ba7a00                   movsxd rcx, dword ptr [rip + 0x7abaa2]
00184822  4869d190040000                   imul rdx, rcx, 0x490
00184829  488d04c9                         lea rax, [rcx + rcx*8]
0018482D  488d0c43                         lea rcx, [rbx + rax*2]
00184831  0fb7841a1c070000                 movzx eax, word ptr [rdx + rbx + 0x71c]
00184839  6639815a3fb200                   cmp word ptr [rcx + 0xb23f5a], ax
00184840  7515                             jne 0x180184857
00184842  0fb7841a1e070000                 movzx eax, word ptr [rdx + rbx + 0x71e]
0018484A  6639815c3fb200                   cmp word ptr [rcx + 0xb23f5c], ax
00184851  7504                             jne 0x180184857
00184853  32c0                             xor al, al
00184855  eb02                             jmp 0x180184859
00184857  b001                             mov al, 1
00184859  8881593fb200                     mov byte ptr [rcx + 0xb23f59], al
0018485F  66452bd8                         sub r11w, r8w
00184863  4863055aba7a00                   movsxd rax, dword ptr [rip + 0x7aba5a]
0018486A  488d0cc0                         lea rcx, [rax + rax*8]
0018486E  6644899c4b623fb200               mov word ptr [rbx + rcx*2 + 0xb23f62], r11w
00184877  48630546ba7a00                   movsxd rax, dword ptr [rip + 0x7aba46]
0018487E  488d0cc0                         lea rcx, [rax + rax*8]
00184882  8bc7                             mov eax, edi
00184884  4c8d044b                         lea r8, [rbx + rcx*2]
00184888  410fbf88623fb200                 movsx ecx, word ptr [r8 + 0xb23f62]
00184890  2bc1                             sub eax, ecx
00184892  99                               cdq 
00184893  33c2                             xor eax, edx
00184895  2bc2                             sub eax, edx
00184897  83f81e                           cmp eax, 0x1e
0018489A  7e08                             jle 0x1801848a4
0018489C  664189b8623fb200                 mov word ptr [r8 + 0xb23f62], di
001848A4  bf01000000                       mov edi, 1
001848A9  48630514ba7a00                   movsxd rax, dword ptr [rip + 0x7aba14]
001848B0  4869c890040000                   imul rcx, rax, 0x490
001848B7  66ff8419d2090000                 inc word ptr [rcx + rbx + 0x9d2]
001848BF  6683bc19d20900000c               cmp word ptr [rcx + rbx + 0x9d2], 0xc
001848C8  0f8cea000000                     jl 0x1801849b8
001848CE  486305efb97a00                   movsxd rax, dword ptr [rip + 0x7ab9ef]
001848D5  4869c890040000                   imul rcx, rax, 0x490
001848DC  6689ac19d2090000                 mov word ptr [rcx + rbx + 0x9d2], bp
001848E4  e9cf000000                       jmp 0x1801849b8
001848E9  486305d4b97a00                   movsxd rax, dword ptr [rip + 0x7ab9d4]
001848F0  498bcc                           mov rcx, r12
001848F3  4869c090040000                   imul rax, rax, 0x490
001848FA  ba09000000                       mov edx, 9
001848FF  480558060000                     add rax, 0x658
00184905  4803c3                           add rax, rbx
00184908  0f1f840000000000                 nop dword ptr [rax + rax]
00184910  488d8080000000                   lea rax, [rax + 0x80]
00184917  0f1001                           movups xmm0, xmmword ptr [rcx]
0018491A  488d8980000000                   lea rcx, [rcx + 0x80]
00184921  0f114080                         movups xmmword ptr [rax - 0x80], xmm0
00184925  0f104990                         movups xmm1, xmmword ptr [rcx - 0x70]
00184929  0f114890                         movups xmmword ptr [rax - 0x70], xmm1
0018492D  0f1041a0                         movups xmm0, xmmword ptr [rcx - 0x60]
00184931  0f1140a0                         movups xmmword ptr [rax - 0x60], xmm0
00184935  0f1049b0                         movups xmm1, xmmword ptr [rcx - 0x50]
00184939  0f1148b0                         movups xmmword ptr [rax - 0x50], xmm1
0018493D  0f1041c0                         movups xmm0, xmmword ptr [rcx - 0x40]
00184941  0f1140c0                         movups xmmword ptr [rax - 0x40], xmm0
00184945  0f1049d0                         movups xmm1, xmmword ptr [rcx - 0x30]
00184949  0f1148d0                         movups xmmword ptr [rax - 0x30], xmm1
0018494D  0f1041e0                         movups xmm0, xmmword ptr [rcx - 0x20]
00184951  0f1140e0                         movups xmmword ptr [rax - 0x20], xmm0
00184955  0f1049f0                         movups xmm1, xmmword ptr [rcx - 0x10]
00184959  0f1148f0                         movups xmmword ptr [rax - 0x10], xmm1
0018495D  4883ea01                         sub rdx, 1
00184961  75ad                             jne 0x180184910
00184963  0f1001                           movups xmm0, xmmword ptr [rcx]
00184966  0f1100                           movups xmmword ptr [rax], xmm0
00184969  e936ffffff                       jmp 0x1801848a4
0018496E  8b1550b97a00                     mov edx, dword ptr [rip + 0x7ab950]
00184974  4533c0                           xor r8d, r8d
00184977  488bcb                           mov rcx, rbx
0018497A  e8e1680100                       call 0x18019b260
0018497F  8b153fb97a00                     mov edx, dword ptr [rip + 0x7ab93f]
00184985  488bcb                           mov rcx, rbx
00184988  e843060000                       call 0x180184fd0
0018498D  e917ffffff                       jmp 0x1801848a9
00184992  99                               cdq 
00184993  412bca                           sub ecx, r10d
00184996  2bc2                             sub eax, edx
00184998  d1f8                             sar eax, 1
0018499A  3bc8                             cmp ecx, eax
0018499C  7e0f                             jle 0x1801849ad
0018499E  664189b874060000                 mov word ptr [r8 + 0x674], di
001849A6  448b0d17b97a00                   mov r9d, dword ptr [rip + 0x7ab917]
001849AD  418bd1                           mov edx, r9d
001849B0  488bcb                           mov rcx, rbx
001849B3  e818060000                       call 0x180184fd0
001849B8  48630505b97a00                   movsxd rax, dword ptr [rip + 0x7ab905]
001849BF  4869d090040000                   imul rdx, rax, 0x490
001849C6  4803d3                           add rdx, rbx
001849C9  8b8288060000                     mov eax, dword ptr [rdx + 0x688]
001849CF  83c0f8                           add eax, -8
001849D2  83f85b                           cmp eax, 0x5b
001849D5  0f87b4030000                     ja 0x180184d8f
001849DB  4898                             cdqe 
001849DD  410fb68406704f1800               movzx eax, byte ptr [r14 + rax + 0x184f70]
001849E6  418b8c863c4f1800                 mov ecx, dword ptr [r14 + rax*4 + 0x184f3c]
001849EE  4903ce                           add rcx, r14
001849F1  ffe1                             jmp rcx
001849F3  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
001849FA  038260060000                     add eax, dword ptr [rdx + 0x660]
00184A00  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184A06  486305b7b87a00                   movsxd rax, dword ptr [rip + 0x7ab8b7]
00184A0D  4869c890040000                   imul rcx, rax, 0x490
00184A14  0fbf8419d6090000                 movsx eax, word ptr [rcx + rbx + 0x9d6]
00184A1C  c1e003                           shl eax, 3
00184A1F  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184A26  48630597b87a00                   movsxd rax, dword ptr [rip + 0x7ab897]
00184A2D  4869c890040000                   imul rcx, rax, 0x490
00184A34  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
00184A3C  6683f84a                         cmp ax, 0x4a
00184A40  7521                             jne 0x180184a63
00184A42  6639ac197c0a0000                 cmp word ptr [rcx + rbx + 0xa7c], bp
00184A4A  0f853f030000                     jne 0x180184d8f
00184A50  8b84195c060000                   mov eax, dword ptr [rcx + rbx + 0x65c]
00184A57  898419cc060000                   mov dword ptr [rcx + rbx + 0x6cc], eax
00184A5E  e92c030000                       jmp 0x180184d8f
00184A63  6683f81c                         cmp ax, 0x1c
00184A67  0f8522030000                     jne 0x180184d8f
00184A6D  f60550b87a0001                   test byte ptr [rip + 0x7ab850], 1
00184A74  0f8415030000                     je 0x180184d8f
00184A7A  8184195c060000d0020000           add dword ptr [rcx + rbx + 0x65c], 0x2d0
00184A85  e905030000                       jmp 0x180184d8f
00184A8A  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184A91  038260060000                     add eax, dword ptr [rdx + 0x660]
00184A97  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184A9D  48630520b87a00                   movsxd rax, dword ptr [rip + 0x7ab820]
00184AA4  4869c890040000                   imul rcx, rax, 0x490
00184AAB  0fbf8419d4090000                 movsx eax, word ptr [rcx + rbx + 0x9d4]
00184AB3  c1e003                           shl eax, 3
00184AB6  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184ABD  e9cd020000                       jmp 0x180184d8f
00184AC2  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184AC9  038260060000                     add eax, dword ptr [rdx + 0x660]
00184ACF  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184AD5  486305e8b77a00                   movsxd rax, dword ptr [rip + 0x7ab7e8]
00184ADC  4869c890040000                   imul rcx, rax, 0x490
00184AE3  0fbf84191e0a0000                 movsx eax, word ptr [rcx + rbx + 0xa1e]
00184AEB  c1e003                           shl eax, 3
00184AEE  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184AF5  e995020000                       jmp 0x180184d8f
00184AFA  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184B01  038260060000                     add eax, dword ptr [rdx + 0x660]
00184B07  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184B0D  486305b0b77a00                   movsxd rax, dword ptr [rip + 0x7ab7b0]
00184B14  4869c890040000                   imul rcx, rax, 0x490
00184B1B  0fbf8419800a0000                 movsx eax, word ptr [rcx + rbx + 0xa80]
00184B23  c1e003                           shl eax, 3
00184B26  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184B2D  e95d020000                       jmp 0x180184d8f
00184B32  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184B39  038260060000                     add eax, dword ptr [rdx + 0x660]
00184B3F  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184B45  48630578b77a00                   movsxd rax, dword ptr [rip + 0x7ab778]
00184B4C  4869d090040000                   imul rdx, rax, 0x490
00184B53  4803d3                           add rdx, rbx
00184B56  0fbf8ad0090000                   movsx ecx, word ptr [rdx + 0x9d0]
00184B5D  0fbf8274060000                   movsx eax, word ptr [rdx + 0x674]
00184B64  03c8                             add ecx, eax
00184B66  c1e103                           shl ecx, 3
00184B69  018a5c060000                     add dword ptr [rdx + 0x65c], ecx
00184B6F  4863154eb77a00                   movsxd rdx, dword ptr [rip + 0x7ab74e]
00184B76  4869ca90040000                   imul rcx, rdx, 0x490
00184B7D  0fb78419e6060000                 movzx eax, word ptr [rcx + rbx + 0x6e6]
00184B85  6683f84a                         cmp ax, 0x4a
00184B89  7418                             je 0x180184ba3
00184B8B  6683f853                         cmp ax, 0x53
00184B8F  7412                             je 0x180184ba3
00184B91  6683f81c                         cmp ax, 0x1c
00184B95  0f85f4010000                     jne 0x180184d8f
00184B9B  f6c201                           test dl, 1
00184B9E  e9d1feffff                       jmp 0x180184a74
00184BA3  0fb784197c0a0000                 movzx eax, word ptr [rcx + rbx + 0xa7c]
00184BAB  66412bc5                         sub ax, r13w
00184BAF  66413bc7                         cmp ax, r15w
00184BB3  0f86d6010000                     jbe 0x180184d8f
00184BB9  8b84195c060000                   mov eax, dword ptr [rcx + rbx + 0x65c]
00184BC0  898419cc060000                   mov dword ptr [rcx + rbx + 0x6cc], eax
00184BC7  e9c3010000                       jmp 0x180184d8f
00184BCC  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184BD3  038260060000                     add eax, dword ptr [rdx + 0x660]
00184BD9  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184BDF  486305deb67a00                   movsxd rax, dword ptr [rip + 0x7ab6de]
00184BE6  4869c890040000                   imul rcx, rax, 0x490
00184BED  0fbf8419d0090000                 movsx eax, word ptr [rcx + rbx + 0x9d0]
00184BF5  c1e002                           shl eax, 2
00184BF8  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184BFF  e98b010000                       jmp 0x180184d8f
00184C04  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184C0B  c1e004                           shl eax, 4
00184C0E  038260060000                     add eax, dword ptr [rdx + 0x660]
00184C14  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184C1A  486305a3b67a00                   movsxd rax, dword ptr [rip + 0x7ab6a3]
00184C21  4869c890040000                   imul rcx, rax, 0x490
00184C28  0fbf8419d6090000                 movsx eax, word ptr [rcx + rbx + 0x9d6]
00184C30  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184C37  e953010000                       jmp 0x180184d8f
00184C3C  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184C43  038260060000                     add eax, dword ptr [rdx + 0x660]
00184C49  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184C4F  4863056eb67a00                   movsxd rax, dword ptr [rip + 0x7ab66e]
00184C56  4869c890040000                   imul rcx, rax, 0x490
00184C5D  0fbf8419d2090000                 movsx eax, word ptr [rcx + rbx + 0x9d2]
00184C65  c1e003                           shl eax, 3
00184C68  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184C6F  e91b010000                       jmp 0x180184d8f
00184C74  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184C7B  038260060000                     add eax, dword ptr [rdx + 0x660]
00184C81  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184C87  48630536b67a00                   movsxd rax, dword ptr [rip + 0x7ab636]
00184C8E  4869c890040000                   imul rcx, rax, 0x490
00184C95  0fbf8419d6090000                 movsx eax, word ptr [rcx + rbx + 0x9d6]
00184C9D  c1e003                           shl eax, 3
00184CA0  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184CA7  48630516b67a00                   movsxd rax, dword ptr [rip + 0x7ab616]
00184CAE  4869d090040000                   imul rdx, rax, 0x490
00184CB5  8b841a60060000                   mov eax, dword ptr [rdx + rbx + 0x660]
00184CBC  0fbf8c1aac060000                 movsx ecx, word ptr [rdx + rbx + 0x6ac]
00184CC4  83e880                           sub eax, -0x80
00184CC7  03c8                             add ecx, eax
00184CC9  898c1acc060000                   mov dword ptr [rdx + rbx + 0x6cc], ecx
00184CD0  486305edb57a00                   movsxd rax, dword ptr [rip + 0x7ab5ed]
00184CD7  4869c890040000                   imul rcx, rax, 0x490
00184CDE  0fbf8419d6090000                 movsx eax, word ptr [rcx + rbx + 0x9d6]
00184CE6  e99a000000                       jmp 0x180184d85
00184CEB  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184CF2  83c009                           add eax, 9
00184CF5  89825c060000                     mov dword ptr [rdx + 0x65c], eax
00184CFB  486305c2b57a00                   movsxd rax, dword ptr [rip + 0x7ab5c2]
00184D02  4869c890040000                   imul rcx, rax, 0x490
00184D09  0fbf8419d6090000                 movsx eax, word ptr [rcx + rbx + 0x9d6]
00184D11  c1e003                           shl eax, 3
00184D14  0184195c060000                   add dword ptr [rcx + rbx + 0x65c], eax
00184D1B  eb72                             jmp 0x180184d8f
00184D1D  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184D24  038260060000                     add eax, dword ptr [rdx + 0x660]
00184D2A  8982d0060000                     mov dword ptr [rdx + 0x6d0], eax
00184D30  4863058db57a00                   movsxd rax, dword ptr [rip + 0x7ab58d]
00184D37  4869c890040000                   imul rcx, rax, 0x490
00184D3E  0fbf8419d0090000                 movsx eax, word ptr [rcx + rbx + 0x9d0]
00184D46  99                               cdq 
00184D47  2bc2                             sub eax, edx
00184D49  d1f8                             sar eax, 1
00184D4B  c1e003                           shl eax, 3
00184D4E  018419d0060000                   add dword ptr [rcx + rbx + 0x6d0], eax
00184D55  eb38                             jmp 0x180184d8f
00184D57  0fbf82ac060000                   movsx eax, word ptr [rdx + 0x6ac]
00184D5E  038260060000                     add eax, dword ptr [rdx + 0x660]
00184D64  8982cc060000                     mov dword ptr [rdx + 0x6cc], eax
00184D6A  48630553b57a00                   movsxd rax, dword ptr [rip + 0x7ab553]
00184D71  4869c890040000                   imul rcx, rax, 0x490
00184D78  0fbf8419d0090000                 movsx eax, word ptr [rcx + rbx + 0x9d0]
00184D80  99                               cdq 
00184D81  2bc2                             sub eax, edx
00184D83  d1f8                             sar eax, 1
00184D85  c1e003                           shl eax, 3
00184D88  018419cc060000                   add dword ptr [rcx + rbx + 0x6cc], eax
00184D8F  4863052eb57a00                   movsxd rax, dword ptr [rip + 0x7ab52e]
00184D96  4869c890040000                   imul rcx, rax, 0x490
00184D9D  0fb78419d4060000                 movzx eax, word ptr [rcx + rbx + 0x6d4]
00184DA5  6685c0                           test ax, ax
00184DA8  740b                             je 0x180184db5
00184DAA  66ffc8                           dec ax
00184DAD  66898419d4060000                 mov word ptr [rcx + rbx + 0x6d4], ax
00184DB5  48630508b57a00                   movsxd rax, dword ptr [rip + 0x7ab508]
00184DBC  4869d090040000                   imul rdx, rax, 0x490
00184DC3  4803d3                           add rdx, rbx
00184DC6  0fb682530a0000                   movzx eax, byte ptr [rdx + 0xa53]
00184DCD  84c0                             test al, al
00184DCF  0f848d000000                     je 0x180184e62
00184DD5  3c01                             cmp al, 1
00184DD7  7513                             jne 0x180184dec
00184DD9  480fbe82520a0000                 movsx rax, byte ptr [rdx + 0xa52]
00184DE1  410fb68486f0c83200               movzx eax, byte ptr [r14 + rax*4 + 0x32c8f0]
00184DEA  eb25                             jmp 0x180184e11
00184DEC  413ac7                           cmp al, r15b
00184DEF  74e8                             je 0x180184dd9
00184DF1  480fbe8a520a0000                 movsx rcx, byte ptr [rdx + 0xa52]
00184DF9  3c03                             cmp al, 3
00184DFB  750b                             jne 0x180184e08
00184DFD  410fb6848eb0c93200               movzx eax, byte ptr [r14 + rcx*4 + 0x32c9b0]
00184E06  eb09                             jmp 0x180184e11
00184E08  410fb6848e90ca3200               movzx eax, byte ptr [r14 + rcx*4 + 0x32ca90]
00184E11  888286090000                     mov byte ptr [rdx + 0x986], al
00184E17  486305a6b47a00                   movsxd rax, dword ptr [rip + 0x7ab4a6]
00184E1E  4869c890040000                   imul rcx, rax, 0x490
00184E25  fe8419520a0000                   inc byte ptr [rcx + rbx + 0xa52]
00184E2C  48630591b47a00                   movsxd rax, dword ptr [rip + 0x7ab491]
00184E33  4869c890040000                   imul rcx, rax, 0x490
00184E3A  4038ac1986090000                 cmp byte ptr [rcx + rbx + 0x986], bpl
00184E42  7f1e                             jg 0x180184e62
00184E44  4088ac19530a0000                 mov byte ptr [rcx + rbx + 0xa53], bpl
00184E4C  48630571b47a00                   movsxd rax, dword ptr [rip + 0x7ab471]
00184E53  4869c890040000                   imul rcx, rax, 0x490
00184E5A  4088ac19520a0000                 mov byte ptr [rcx + rbx + 0xa52], bpl
00184E62  41b96f000000                     mov r9d, 0x6f
00184E68  8b1556b47a00                     mov edx, dword ptr [rip + 0x7ab456]
00184E6E  ffc2                             inc edx
00184E70  89154eb47a00                     mov dword ptr [rip + 0x7ab44e], edx
00184E76  3b13                             cmp edx, dword ptr [rbx]
00184E78  0f8cd2edffff                     jl 0x180183c50
00184E7E  392db885f205                     cmp dword ptr [rip + 0x5f285b8], ebp
00184E84  7508                             jne 0x180184e8e
00184E86  892d34b47a00                     mov dword ptr [rip + 0x7ab434], ebp
00184E8C  eb06                             jmp 0x180184e94
00184E8E  ff052cb47a00                     inc dword ptr [rip + 0x7ab42c]
00184E94  488b5c2460                       mov rbx, qword ptr [rsp + 0x60]
00184E99  488b6c2468                       mov rbp, qword ptr [rsp + 0x68]
00184E9E  488b742470                       mov rsi, qword ptr [rsp + 0x70]
00184EA3  4883c430                         add rsp, 0x30
00184EA7  415f                             pop r15
00184EA9  415e                             pop r14
00184EAB  415d                             pop r13
00184EAD  415c                             pop r12
00184EAF  5f                               pop rdi
00184EB0  c3                               ret 

wild distance writer: RVA 0x18C040, full extent 285, role confidence candidate
0018C040  48895c2408                       mov qword ptr [rsp + 8], rbx
0018C045  48896c2410                       mov qword ptr [rsp + 0x10], rbp
0018C04A  4889742418                       mov qword ptr [rsp + 0x18], rsi
0018C04F  48897c2420                       mov qword ptr [rsp + 0x20], rdi
0018C054  4863c2                           movsxd rax, edx
0018C057  488bf9                           mov rdi, rcx
0018C05A  4869d090040000                   imul rdx, rax, 0x490
0018C061  833d288b3e0800                   cmp dword ptr [rip + 0x83e8b28], 0
0018C068  bb10270000                       mov ebx, 0x2710
0018C06D  0fbfb40a0e070000                 movsx esi, word ptr [rdx + rcx + 0x70e]
0018C075  0fbfac0a10070000                 movsx ebp, word ptr [rdx + rcx + 0x710]
0018C07D  0f85c3000000                     jne 0x18018c146
0018C083  83790464                         cmp dword ptr [rcx + 4], 0x64
0018C087  0f8fb9000000                     jg 0x18018c146
0018C08D  480fbf8c0aee060000               movsx rcx, word ptr [rdx + rcx + 0x6ee]
0018C096  488d15bb0d6103                   lea rdx, [rip + 0x3610dbb]
0018C09D  4869c13c580000                   imul rax, rcx, 0x583c
0018C0A4  4c631c10                         movsxd r11, dword ptr [rax + rdx]
0018C0A8  4d85db                           test r11, r11
0018C0AB  0f8e95000000                     jle 0x18018c146
0018C0B1  4c69d1409c0000                   imul r10, rcx, 0x9c40
0018C0B8  4869c9204e0000                   imul rcx, rcx, 0x4e20
0018C0BF  488d0542187603                   lea rax, [rip + 0x3761842]
0018C0C6  4c03d0                           add r10, rax
0018C0C9  488d0518597303                   lea rax, [rip + 0x3735918]
0018C0D0  4803c8                           add rcx, rax
0018C0D3  0f1f4000                         nop dword ptr [rax]
0018C0D7  660f1f840000000000               nop word ptr [rax + rax]
0018C0E0  480fbf01                         movsx rax, word ptr [rcx]
0018C0E4  4c69c090040000                   imul r8, rax, 0x490
0018C0EB  418b02                           mov eax, dword ptr [r10]
0018C0EE  41398438f0060000                 cmp dword ptr [r8 + rdi + 0x6f0], eax
0018C0F6  7540                             jne 0x18018c138
0018C0F8  410fbf94380e070000               movsx edx, word ptr [r8 + rdi + 0x70e]
0018C101  448bce                           mov r9d, esi
0018C104  442bca                           sub r9d, edx
0018C107  8bc2                             mov eax, edx
0018C109  2bc6                             sub eax, esi
0018C10B  3bf2                             cmp esi, edx
0018C10D  410fbf943810070000               movsx edx, word ptr [r8 + rdi + 0x710]
0018C116  448bc5                           mov r8d, ebp
0018C119  440f4ec8                         cmovle r9d, eax
0018C11D  442bc2                           sub r8d, edx
0018C120  8bc2                             mov eax, edx
0018C122  2bc5                             sub eax, ebp
0018C124  3bea                             cmp ebp, edx
0018C126  440f4ec0                         cmovle r8d, eax
0018C12A  453bc8                           cmp r9d, r8d
0018C12D  450f4cc8                         cmovl r9d, r8d
0018C131  443bcb                           cmp r9d, ebx
0018C134  410f4cd9                         cmovl ebx, r9d
0018C138  4883c102                         add rcx, 2
0018C13C  4983c204                         add r10, 4
0018C140  4983eb01                         sub r11, 1
0018C144  759a                             jne 0x18018c0e0
0018C146  488b6c2410                       mov rbp, qword ptr [rsp + 0x10]
0018C14B  8bc3                             mov eax, ebx
0018C14D  488b5c2408                       mov rbx, qword ptr [rsp + 8]
0018C152  488b742418                       mov rsi, qword ptr [rsp + 0x18]
0018C157  488b7c2420                       mov rdi, qword ptr [rsp + 0x20]
0018C15C  c3                               ret 

enemy distance writer: RVA 0x18C160, full extent 2665, role confidence candidate
0018C160  89542410                         mov dword ptr [rsp + 0x10], edx
0018C164  48894c2408                       mov qword ptr [rsp + 8], rcx
0018C169  53                               push rbx
0018C16A  55                               push rbp
0018C16B  56                               push rsi
0018C16C  57                               push rdi
0018C16D  4154                             push r12
0018C16F  4155                             push r13
0018C171  4156                             push r14
0018C173  4157                             push r15
0018C175  4881ec98000000                   sub rsp, 0x98
0018C17C  4533d2                           xor r10d, r10d
0018C17F  4863c2                           movsxd rax, edx
0018C182  4c69e890040000                   imul r13, rax, 0x490
0018C189  488bf1                           mov rsi, rcx
0018C18C  4489542454                       mov dword ptr [rsp + 0x54], r10d
0018C191  bf801a0600                       mov edi, 0x61a80
0018C196  4c896c2478                       mov qword ptr [rsp + 0x78], r13
0018C19B  897c2440                         mov dword ptr [rsp + 0x40], edi
0018C19F  458bf2                           mov r14d, r10d
0018C1A2  4489542450                       mov dword ptr [rsp + 0x50], r10d
0018C1A7  418bea                           mov ebp, r10d
0018C1AA  4a6384292c070000                 movsxd rax, dword ptr [rcx + r13 + 0x72c]
0018C1B2  460fbfbc290e070000               movsx r15d, word ptr [rcx + r13 + 0x70e]
0018C1BB  460fbfa42910070000               movsx r12d, word ptr [rcx + r13 + 0x710]
0018C1C4  4a0fbf9c29ee060000               movsx rbx, word ptr [rcx + r13 + 0x6ee]
0018C1CD  488d0ddc2bed03                   lea rcx, [rip + 0x3ed2bdc]
0018C1D4  0fbf8441e0d80801                 movsx eax, word ptr [rcx + rax*2 + 0x108d8e0]
0018C1DC  89442464                         mov dword ptr [rsp + 0x64], eax
0018C1E0  44897c2430                       mov dword ptr [rsp + 0x30], r15d
0018C1E5  4489642434                       mov dword ptr [rsp + 0x34], r12d
0018C1EA  44899424f8000000                 mov dword ptr [rsp + 0xf8], r10d
0018C1F2  4489542438                       mov dword ptr [rsp + 0x38], r10d
0018C1F7  664639942ef8080000               cmp word ptr [rsi + r13 + 0x8f8], r10w
0018C200  7407                             je 0x18018c209
0018C202  33c0                             xor eax, eax
0018C204  e9ac090000                       jmp 0x18018cbb5
0018C209  c7461ca0860100                   mov dword ptr [rsi + 0x1c], 0x186a0
0018C210  4c8d05e93de7ff                   lea r8, [rip - 0x18c217]
0018C217  664639942e540a0000               cmp word ptr [rsi + r13 + 0xa54], r10w
0018C220  7517                             jne 0x18018c239
0018C222  664639942e9e090000               cmp word ptr [rsi + r13 + 0x99e], r10w
0018C22B  7462                             je 0x18018c28f
0018C22D  664283bc2e1809000065             cmp word ptr [rsi + r13 + 0x918], 0x65
0018C237  7556                             jne 0x18018c28f
0018C239  4a0fbf842e30090000               movsx rax, word ptr [rsi + r13 + 0x930]
0018C242  6685c0                           test ax, ax
0018C245  7448                             je 0x18018c28f
0018C247  4869c888060000                   imul rcx, rax, 0x688
0018C24E  420fbf9401546dcc07               movsx edx, word ptr [rcx + r8 + 0x7cc6d54]
0018C257  420fbf8401a26ccc07               movsx eax, word ptr [rcx + r8 + 0x7cc6ca2]
0018C260  89442438                         mov dword ptr [rsp + 0x38], eax
0018C264  83ea01                           sub edx, 1
0018C267  7413                             je 0x18018c27c
0018C269  83fa01                           cmp edx, 1
0018C26C  8b9424e8000000                   mov edx, dword ptr [rsp + 0xe8]
0018C273  751a                             jne 0x18018c28f
0018C275  bdc8000000                       mov ebp, 0xc8
0018C27A  eb0c                             jmp 0x18018c288
0018C27C  8b9424e8000000                   mov edx, dword ptr [rsp + 0xe8]
0018C283  bd28000000                       mov ebp, 0x28
0018C288  89ac24f8000000                   mov dword ptr [rsp + 0xf8], ebp
0018C28F  420fb7842ef4090000               movzx eax, word ptr [rsi + r13 + 0x9f4]
0018C298  41b901000000                     mov r9d, 1
0018C29E  6683f804                         cmp ax, 4
0018C2A2  0f8585000000                     jne 0x18018c32d
0018C2A8  420fbf842ee6060000               movsx eax, word ptr [rsi + r13 + 0x6e6]
0018C2B1  458bf1                           mov r14d, r9d
0018C2B4  83c0fb                           add eax, -5
0018C2B7  83f850                           cmp eax, 0x50
0018C2BA  0f8768010000                     ja 0x18018c428
0018C2C0  4898                             cdqe 
0018C2C2  410fb68400d4cb1800               movzx eax, byte ptr [r8 + rax + 0x18cbd4]
0018C2CB  418b8c80cccb1800                 mov ecx, dword ptr [r8 + rax*4 + 0x18cbcc]
0018C2D3  4903c8                           add rcx, r8
0018C2D6  ffe1                             jmp rcx
0018C2D8  4a0fbf842e30090000               movsx rax, word ptr [rsi + r13 + 0x930]
0018C2E1  458bf2                           mov r14d, r10d
0018C2E4  6685c0                           test ax, ax
0018C2E7  7431                             je 0x18018c31a
0018C2E9  4869d088060000                   imul rdx, rax, 0x688
0018C2F0  4a0fbf84025c6dcc07               movsx rax, word ptr [rdx + r8 + 0x7cc6d5c]
0018C2F9  6685c0                           test ax, ax
0018C2FC  741c                             je 0x18018c31a
0018C2FE  4869c890040000                   imul rcx, rax, 0x490
0018C305  428b8402606dcc07                 mov eax, dword ptr [rdx + r8 + 0x7cc6d60]
0018C30D  398431f0060000                   cmp dword ptr [rcx + rsi + 0x6f0], eax
0018C314  0f840b010000                     je 0x18018c425
0018C31A  b803000000                       mov eax, 3
0018C31F  664289842ef4090000               mov word ptr [rsi + r13 + 0x9f4], ax
0018C328  e9fb000000                       jmp 0x18018c428
0018C32D  6683f805                         cmp ax, 5
0018C331  0f85f1000000                     jne 0x18018c428
0018C337  420fbf842ee6060000               movsx eax, word ptr [rsi + r13 + 0x6e6]
0018C340  83c0e8                           add eax, -0x18
0018C343  83f83d                           cmp eax, 0x3d
0018C346  0f87dc000000                     ja 0x18018c428
0018C34C  4898                             cdqe 
0018C34E  410fb6840030cc1800               movzx eax, byte ptr [r8 + rax + 0x18cc30]
0018C357  418b8c8028cc1800                 mov ecx, dword ptr [r8 + rax*4 + 0x18cc28]
0018C35F  4903c8                           add rcx, r8
0018C362  ffe1                             jmp rcx
0018C364  664639942e040a0000               cmp word ptr [rsi + r13 + 0xa04], r10w
0018C36D  0f8cb2000000                     jl 0x18018c425
0018C373  664283bc2e180900006a             cmp word ptr [rsi + r13 + 0x918], 0x6a
0018C37D  0f84a2000000                     je 0x18018c425
0018C383  420fbf8c2e400a0000               movsx ecx, word ptr [rsi + r13 + 0xa40]
0018C38C  66423b8c2e1c070000               cmp cx, word ptr [rsi + r13 + 0x71c]
0018C395  751f                             jne 0x18018c3b6
0018C397  420fb7842e1e070000               movzx eax, word ptr [rsi + r13 + 0x71e]
0018C3A0  664239842e420a0000               cmp word ptr [rsi + r13 + 0xa42], ax
0018C3A9  750b                             jne 0x18018c3b6
0018C3AB  664689942ef4090000               mov word ptr [rsi + r13 + 0x9f4], r10w
0018C3B4  eb72                             jmp 0x18018c428
0018C3B6  420fbf842e420a0000               movsx eax, word ptr [rsi + r13 + 0xa42]
0018C3BF  448bc1                           mov r8d, ecx
0018C3C2  6642898c2e34090000               mov word ptr [rsi + r13 + 0x934], cx
0018C3CB  448bc8                           mov r9d, eax
0018C3CE  488bce                           mov rcx, rsi
0018C3D1  664289842e36090000               mov word ptr [rsi + r13 + 0x936], ax
0018C3DA  4489542420                       mov dword ptr [rsp + 0x20], r10d
0018C3DF  e89c9e0000                       call 0x180196280
0018C3E4  b865000000                       mov eax, 0x65
0018C3E9  4c8d05103ce7ff                   lea r8, [rip - 0x18c3f0]
0018C3F0  664289842e18090000               mov word ptr [rsi + r13 + 0x918], ax
0018C3F9  b9ceffffff                       mov ecx, 0xffffffce
0018C3FE  420fbf842e54070000               movsx eax, word ptr [rsi + r13 + 0x754]
0018C407  41b901000000                     mov r9d, 1
0018C40D  99                               cdq 
0018C40E  2bc2                             sub eax, edx
0018C410  d1f8                             sar eax, 1
0018C412  f7d8                             neg eax
0018C414  3bc1                             cmp eax, ecx
0018C416  0f4cc8                           cmovl ecx, eax
0018C419  4533d2                           xor r10d, r10d
0018C41C  6642898c2e040a0000               mov word ptr [rsi + r13 + 0xa04], cx
0018C425  458bf1                           mov r14d, r9d
0018C428  664383bc28fc8c7e0600             cmp word ptr [r8 + r13 + 0x67e8cfc], 0
0018C432  488bd3                           mov rdx, rbx
0018C435  450f44f1                         cmove r14d, r9d
0018C439  4869c33c580000                   imul rax, rbx, 0x583c
0018C440  4489742448                       mov dword ptr [rsp + 0x48], r14d
0018C445  488d8058ce7903                   lea rax, [rax + 0x379ce58]
0018C44C  4903c0                           add rax, r8
0018C44F  4889842480000000                 mov qword ptr [rsp + 0x80], rax
0018C457  8b08                             mov ecx, dword ptr [rax]
0018C459  8d41fb                           lea eax, [rcx - 5]
0018C45C  83f809                           cmp eax, 9
0018C45F  770a                             ja 0x18018c46b
0018C461  c744243c02000000                 mov dword ptr [rsp + 0x3c], 2
0018C469  eb1a                             jmp 0x18018c485
0018C46B  41b805000000                     mov r8d, 5
0018C471  413bc8                           cmp ecx, r8d
0018C474  450f4dc1                         cmovge r8d, r9d
0018C478  448944243c                       mov dword ptr [rsp + 0x3c], r8d
0018C47D  85c9                             test ecx, ecx
0018C47F  0f8ebd040000                     jle 0x18018c942
0018C485  4869da409c0000                   imul rbx, rdx, 0x9c40
0018C48C  488d0575147603                   lea rax, [rip + 0x3761475]
0018C493  448954244c                       mov dword ptr [rsp + 0x4c], r10d
0018C498  4803d8                           add rbx, rax
0018C49B  488d0d46557303                   lea rcx, [rip + 0x3735546]
0018C4A2  4869c2204e0000                   imul rax, rdx, 0x4e20
0018C4A9  48895c2468                       mov qword ptr [rsp + 0x68], rbx
0018C4AE  4803c1                           add rax, rcx
0018C4B1  4889442470                       mov qword ptr [rsp + 0x70], rax
0018C4B6  66660f1f840000000000             nop word ptr [rax + rax]
0018C4C0  4c0fbf18                         movsx r11, word ptr [rax]
0018C4C4  418bc7                           mov eax, r15d
0018C4C7  4d69d390040000                   imul r10, r11, 0x490
0018C4CE  44895c2460                       mov dword ptr [rsp + 0x60], r11d
0018C4D3  4c03d6                           add r10, rsi
0018C4D6  410fbf8a0e070000                 movsx ecx, word ptr [r10 + 0x70e]
0018C4DE  2bc1                             sub eax, ecx
0018C4E0  448bc1                           mov r8d, ecx
0018C4E3  452bc7                           sub r8d, r15d
0018C4E6  443bf9                           cmp r15d, ecx
0018C4E9  410fbf8a10070000                 movsx ecx, word ptr [r10 + 0x710]
0018C4F1  448bc9                           mov r9d, ecx
0018C4F4  440f4fc0                         cmovg r8d, eax
0018C4F8  452bcc                           sub r9d, r12d
0018C4FB  418bc4                           mov eax, r12d
0018C4FE  2bc1                             sub eax, ecx
0018C500  443be1                           cmp r12d, ecx
0018C503  440f4fc8                         cmovg r9d, eax
0018C507  453bc1                           cmp r8d, r9d
0018C50A  7d22                             jge 0x18018c52e
0018C50C  438d0c00                         lea ecx, [r8 + r8]
0018C510  b867666666                       mov eax, 0x66666667
0018C515  f7e9                             imul ecx
0018C517  d1fa                             sar edx, 1
0018C519  8bc2                             mov eax, edx
0018C51B  c1e81f                           shr eax, 0x1f
0018C51E  03c2                             add eax, edx
0018C520  410fafc0                         imul eax, r8d
0018C524  99                               cdq 
0018C525  41f7f9                           idiv r9d
0018C528  418d0c01                         lea ecx, [r9 + rax]
0018C52C  eb29                             jmp 0x18018c557
0018C52E  4585c0                           test r8d, r8d
0018C531  7422                             je 0x18018c555
0018C533  438d0c09                         lea ecx, [r9 + r9]
0018C537  b867666666                       mov eax, 0x66666667
0018C53C  f7e9                             imul ecx
0018C53E  d1fa                             sar edx, 1
0018C540  8bc2                             mov eax, edx
0018C542  c1e81f                           shr eax, 0x1f
0018C545  03c2                             add eax, edx
0018C547  410fafc1                         imul eax, r9d
0018C54B  99                               cdq 
0018C54C  41f7f8                           idiv r8d
0018C54F  418d0c00                         lea ecx, [r8 + rax]
0018C553  eb02                             jmp 0x18018c557
0018C555  33c9                             xor ecx, ecx
0018C557  3bcf                             cmp ecx, edi
0018C559  8bc1                             mov eax, ecx
0018C55B  baf7ff0000                       mov edx, 0xfff7
0018C560  0f4dc7                           cmovge eax, edi
0018C563  8bf8                             mov edi, eax
0018C565  89442444                         mov dword ptr [rsp + 0x44], eax
0018C569  420fb7842ee6060000               movzx eax, word ptr [rsi + r13 + 0x6e6]
0018C572  6683e849                         sub ax, 0x49
0018C576  6685c2                           test dx, ax
0018C579  754d                             jne 0x18018c5c8
0018C57B  664183bafc08000000               cmp word ptr [r10 + 0x8fc], 0
0018C584  7442                             je 0x18018c5c8
0018C586  410fbf82e6060000                 movsx eax, word ptr [r10 + 0x6e6]
0018C58E  83c0d9                           add eax, -0x27
0018C591  83f826                           cmp eax, 0x26
0018C594  772a                             ja 0x18018c5c0
0018C596  4c8d05633ae7ff                   lea r8, [rip - 0x18c59d]
0018C59D  4898                             cdqe 
0018C59F  410fb6840078cc1800               movzx eax, byte ptr [r8 + rax + 0x18cc78]
0018C5A8  418b948070cc1800                 mov edx, dword ptr [r8 + rax*4 + 0x18cc70]
0018C5B0  4903d0                           add rdx, r8
0018C5B3  ffe2                             jmp rdx
0018C5B5  664183ba0c0a000000               cmp word ptr [r10 + 0xa0c], 0
0018C5BE  7e08                             jle 0x18018c5c8
0018C5C0  3b4e1c                           cmp ecx, dword ptr [rsi + 0x1c]
0018C5C3  7d03                             jge 0x18018c5c8
0018C5C5  894e1c                           mov dword ptr [rsi + 0x1c], ecx
0018C5C8  4585f6                           test r14d, r14d
0018C5CB  0f8539030000                     jne 0x18018c90a
0018C5D1  85ed                             test ebp, ebp
0018C5D3  0f8431030000                     je 0x18018c90a
0018C5D9  664639b42e040a0000               cmp word ptr [rsi + r13 + 0xa04], r14w
0018C5E2  0f8c22030000                     jl 0x18018c90a
0018C5E8  420fbf842e48070000               movsx eax, word ptr [rsi + r13 + 0x748]
0018C5F1  6685c0                           test ax, ax
0018C5F4  7459                             je 0x18018c64f
0018C5F6  4439742438                       cmp dword ptr [rsp + 0x38], r14d
0018C5FB  7552                             jne 0x18018c64f
0018C5FD  83fd28                           cmp ebp, 0x28
0018C600  754d                             jne 0x18018c64f
0018C602  410fbf8a0e070000                 movsx ecx, word ptr [r10 + 0x70e]
0018C60A  448bc0                           mov r8d, eax
0018C60D  460fbf8c2e4a070000               movsx r9d, word ptr [rsi + r13 + 0x74a]
0018C616  8bd1                             mov edx, ecx
0018C618  41c1e003                         shl r8d, 3
0018C61C  41c1e103                         shl r9d, 3
0018C620  418bc0                           mov eax, r8d
0018C623  2bc1                             sub eax, ecx
0018C625  412bc8                           sub ecx, r8d
0018C628  413bd0                           cmp edx, r8d
0018C62B  458bc1                           mov r8d, r9d
0018C62E  0f4ec8                           cmovle ecx, eax
0018C631  410fbf8210070000                 movsx eax, word ptr [r10 + 0x710]
0018C639  442bc0                           sub r8d, eax
0018C63C  8bd0                             mov edx, eax
0018C63E  412bd1                           sub edx, r9d
0018C641  413bc1                           cmp eax, r9d
0018C644  440f4fc2                         cmovg r8d, edx
0018C648  413bc8                           cmp ecx, r8d
0018C64B  410f4cc8                         cmovl ecx, r8d
0018C64F  3bcd                             cmp ecx, ebp
0018C651  0f8fb3020000                     jg 0x18018c90a
0018C657  8b03                             mov eax, dword ptr [rbx]
0018C659  413982f0060000                   cmp dword ptr [r10 + 0x6f0], eax
0018C660  0f85a4020000                     jne 0x18018c90a
0018C666  450fbf8ae6060000                 movsx r9d, word ptr [r10 + 0x6e6]
0018C66E  4183f951                         cmp r9d, 0x51
0018C672  752b                             jne 0x18018c69f
0018C674  4183ba900a000050                 cmp dword ptr [r10 + 0xa90], 0x50
0018C67C  7e21                             jle 0x18018c69f
0018C67E  410fb78218090000                 movzx eax, word ptr [r10 + 0x918]
0018C686  6683e86a                         sub ax, 0x6a
0018C68A  6683f801                         cmp ax, 1
0018C68E  760f                             jbe 0x18018c69f
0018C690  664183ba9c0a000078               cmp word ptr [r10 + 0xa9c], 0x78
0018C699  0f8f6b020000                     jg 0x18018c90a
0018C69F  468b842edc090000                 mov r8d, dword ptr [rsi + r13 + 0x9dc]
0018C6A7  488d2d5239e7ff                   lea rbp, [rip - 0x18c6ae]
0018C6AE  4503c3                           add r8d, r11d
0018C6B1  894c245c                         mov dword ptr [rsp + 0x5c], ecx
0018C6B5  b867666666                       mov eax, 0x66666667
0018C6BA  41f7e8                           imul r8d
0018C6BD  c1fa02                           sar edx, 2
0018C6C0  8bc2                             mov eax, edx
0018C6C2  c1e81f                           shr eax, 0x1f
0018C6C5  03d0                             add edx, eax
0018C6C7  8d0492                           lea eax, [rdx + rdx*4]
0018C6CA  03c0                             add eax, eax
0018C6CC  442bc0                           sub r8d, eax
0018C6CF  4963c0                           movsxd rax, r8d
0018C6D2  8b8485c0193200                   mov eax, dword ptr [rbp + rax*4 + 0x3219c0]
0018C6D9  99                               cdq 
0018C6DA  f77c243c                         idiv dword ptr [rsp + 0x3c]
0018C6DE  03c8                             add ecx, eax
0018C6E0  664183ba180900006a               cmp word ptr [r10 + 0x918], 0x6a
0018C6E9  751a                             jne 0x18018c705
0018C6EB  450fbf82060a0000                 movsx r8d, word ptr [r10 + 0xa06]
0018C6F3  416bd032                         imul edx, r8d, 0x32
0018C6F7  03ca                             add ecx, edx
0018C6F9  03c9                             add ecx, ecx
0018C6FB  4183f802                         cmp r8d, 2
0018C6FF  0f8f05020000                     jg 0x18018c90a
0018C705  450fbf820a0a0000                 movsx r8d, word ptr [r10 + 0xa0a]
0018C70D  4183f801                         cmp r8d, 1
0018C711  7e1e                             jle 0x18018c731
0018C713  416bd032                         imul edx, r8d, 0x32
0018C717  03ca                             add ecx, edx
0018C719  4183f803                         cmp r8d, 3
0018C71D  7c12                             jl 0x18018c731
0018C71F  420fbf842e9e090000               movsx eax, word ptr [rsi + r13 + 0x99e]
0018C728  413bc3                           cmp eax, r11d
0018C72B  0f85d9010000                     jne 0x18018c90a
0018C731  4180ba8409000000                 cmp byte ptr [r10 + 0x984], 0
0018C739  7422                             je 0x18018c75d
0018C73B  418d41d3                         lea eax, [r9 - 0x2d]
0018C73F  6683f82a                         cmp ax, 0x2a
0018C743  0f87c1010000                     ja 0x18018c90a
0018C749  48ba0100400000040000             movabs rdx, 0x40000400001
0018C753  480fa3c2                         bt rdx, rax
0018C757  0f83ad010000                     jae 0x18018c90a
0018C75D  418d41ff                         lea eax, [r9 - 1]
0018C761  83f854                           cmp eax, 0x54
0018C764  773b                             ja 0x18018c7a1
0018C766  4898                             cdqe 
0018C768  0fb68428b8cc1800                 movzx eax, byte ptr [rax + rbp + 0x18ccb8]
0018C770  8b9485a0cc1800                   mov edx, dword ptr [rbp + rax*4 + 0x18cca0]
0018C777  4803d5                           add rdx, rbp
0018C77A  ffe2                             jmp rdx
0018C77C  8bc1                             mov eax, ecx
0018C77E  99                               cdq 
0018C77F  83e203                           and edx, 3
0018C782  8d0c02                           lea ecx, [rdx + rax]
0018C785  c1f902                           sar ecx, 2
0018C788  eb1e                             jmp 0x18018c7a8
0018C78A  83c119                           add ecx, 0x19
0018C78D  eb19                             jmp 0x18018c7a8
0018C78F  8d0c4d64000000                   lea ecx, [rcx*2 + 0x64]
0018C796  eb10                             jmp 0x18018c7a8
0018C798  8d0ccdc8000000                   lea ecx, [rcx*8 + 0xc8]
0018C79F  eb07                             jmp 0x18018c7a8
0018C7A1  8d0c8dc8000000                   lea ecx, [rcx*4 + 0xc8]
0018C7A8  460fbf842e9e090000               movsx r8d, word ptr [rsi + r13 + 0x99e]
0018C7B1  8bc1                             mov eax, ecx
0018C7B3  99                               cdq 
0018C7B4  2bc2                             sub eax, edx
0018C7B6  d1f8                             sar eax, 1
0018C7B8  453bc3                           cmp r8d, r11d
0018C7BB  0f45c1                           cmovne eax, ecx
0018C7BE  89442458                         mov dword ptr [rsp + 0x58], eax
0018C7C2  3b442440                         cmp eax, dword ptr [rsp + 0x40]
0018C7C6  0f8d3e010000                     jge 0x18018c90a
0018C7CC  410fbfaa14070000                 movsx ebp, word ptr [r10 + 0x714]
0018C7D4  488d35b5521900                   lea rsi, [rip + 0x1952b5]
0018C7DB  410fbf8a12070000                 movsx ecx, word ptr [r10 + 0x712]
0018C7E3  41bf10270000                     mov r15d, 0x2710
0018C7E9  410fbf921e070000                 movsx edx, word ptr [r10 + 0x71e]
0018C7F1  03e9                             add ebp, ecx
0018C7F3  458b822c070000                   mov r8d, dword ptr [r10 + 0x72c]
0018C7FA  33db                             xor ebx, ebx
0018C7FC  44898424f0000000                 mov dword ptr [rsp + 0xf0], r8d
0018C804  448d2cd500000000                 lea r13d, [rdx*8]
0018C80C  448d63ff                         lea r12d, [rbx - 1]
0018C810  8b06                             mov eax, dword ptr [rsi]
0018C812  488d0d9725ed03                   lea rcx, [rip + 0x3ed2597]
0018C819  4103c5                           add eax, r13d
0018C81C  4898                             cdqe 
0018C81E  8b3c81                           mov edi, dword ptr [rcx + rax*4]
0018C821  4103f8                           add edi, r8d
0018C824  4863c7                           movsxd rax, edi
0018C827  4c8d3400                         lea r14, [rax + rax]
0018C82B  664183bc0e006cbf0000             cmp word ptr [r14 + rcx + 0xbf6c00], 0
0018C835  0f857e000000                     jne 0x18018c8b9
0018C83B  8bd7                             mov edx, edi
0018C83D  e8cef0edff                       call 0x18006b910
0018C842  8d4810                           lea ecx, [rax + 0x10]
0018C845  3be9                             cmp ebp, ecx
0018C847  7d68                             jge 0x18018c8b1
0018C849  83c0f0                           add eax, -0x10
0018C84C  3be8                             cmp ebp, eax
0018C84E  7e61                             jle 0x18018c8b1
0018C850  448b4c2434                       mov r9d, dword ptr [rsp + 0x34]
0018C855  4c8d05a437e7ff                   lea r8, [rip - 0x18c85c]
0018C85C  4b0fbf9406a4e2aa03               movsx rdx, word ptr [r14 + r8 + 0x3aae2a4]
0018C865  488d0c52                         lea rcx, [rdx + rdx*2]
0018C869  412bbc882cff0204                 sub edi, dword ptr [r8 + rcx*4 + 0x402ff2c]
0018C871  8d14d500000000                   lea edx, [rdx*8]
0018C878  8b4c2430                         mov ecx, dword ptr [rsp + 0x30]
0018C87C  448bc1                           mov r8d, ecx
0018C87F  c1e703                           shl edi, 3
0018C882  442bc7                           sub r8d, edi
0018C885  8bc7                             mov eax, edi
0018C887  2bc1                             sub eax, ecx
0018C889  3bcf                             cmp ecx, edi
0018C88B  418bc9                           mov ecx, r9d
0018C88E  440f4ec0                         cmovle r8d, eax
0018C892  2bca                             sub ecx, edx
0018C894  8bc2                             mov eax, edx
0018C896  412bc1                           sub eax, r9d
0018C899  443bca                           cmp r9d, edx
0018C89C  0f4ec8                           cmovle ecx, eax
0018C89F  443bc1                           cmp r8d, ecx
0018C8A2  440f4cc1                         cmovl r8d, ecx
0018C8A6  453bc7                           cmp r8d, r15d
0018C8A9  7d06                             jge 0x18018c8b1
0018C8AB  458bf8                           mov r15d, r8d
0018C8AE  448be3                           mov r12d, ebx
0018C8B1  448b8424f0000000                 mov r8d, dword ptr [rsp + 0xf0]
0018C8B9  ffc3                             inc ebx
0018C8BB  4883c604                         add rsi, 4
0018C8BF  83fb08                           cmp ebx, 8
0018C8C2  0f8c48ffffff                     jl 0x18018c810
0018C8C8  4c8b6c2478                       mov r13, qword ptr [rsp + 0x78]
0018C8CD  4585e4                           test r12d, r12d
0018C8D0  448b642434                       mov r12d, dword ptr [rsp + 0x34]
0018C8D5  8b7c2444                         mov edi, dword ptr [rsp + 0x44]
0018C8D9  488bb424e0000000                 mov rsi, qword ptr [rsp + 0xe0]
0018C8E1  488b5c2468                       mov rbx, qword ptr [rsp + 0x68]
0018C8E6  448b742448                       mov r14d, dword ptr [rsp + 0x48]
0018C8EB  448b7c2430                       mov r15d, dword ptr [rsp + 0x30]
0018C8F0  7818                             js 0x18018c90a
0018C8F2  8b442458                         mov eax, dword ptr [rsp + 0x58]
0018C8F6  89442440                         mov dword ptr [rsp + 0x40], eax
0018C8FA  8b44245c                         mov eax, dword ptr [rsp + 0x5c]
0018C8FE  89442454                         mov dword ptr [rsp + 0x54], eax
0018C902  8b442460                         mov eax, dword ptr [rsp + 0x60]
0018C906  89442450                         mov dword ptr [rsp + 0x50], eax
0018C90A  8b54244c                         mov edx, dword ptr [rsp + 0x4c]
0018C90E  4883c304                         add rbx, 4
0018C912  488b442470                       mov rax, qword ptr [rsp + 0x70]
0018C917  ffc2                             inc edx
0018C919  488b8c2480000000                 mov rcx, qword ptr [rsp + 0x80]
0018C921  4883c002                         add rax, 2
0018C925  8bac24f8000000                   mov ebp, dword ptr [rsp + 0xf8]
0018C92C  8954244c                         mov dword ptr [rsp + 0x4c], edx
0018C930  4889442470                       mov qword ptr [rsp + 0x70], rax
0018C935  48895c2468                       mov qword ptr [rsp + 0x68], rbx
0018C93A  3b11                             cmp edx, dword ptr [rcx]
0018C93C  0f8c7efbffff                     jl 0x18018c4c0
0018C942  83bc24f800000000                 cmp dword ptr [rsp + 0xf8], 0
0018C94A  0f84e0010000                     je 0x18018cb30
0018C950  664283bc2e040a000000             cmp word ptr [rsi + r13 + 0xa04], 0
0018C95A  0f8c49020000                     jl 0x18018cba9
0018C960  4585f6                           test r14d, r14d
0018C963  0f85c7010000                     jne 0x18018cb30
0018C969  48636c2450                       movsxd rbp, dword ptr [rsp + 0x50]
0018C96E  85ed                             test ebp, ebp
0018C970  0f848d010000                     je 0x18018cb03
0018C976  420fbf842e9e090000               movsx eax, word ptr [rsi + r13 + 0x99e]
0018C97F  3be8                             cmp ebp, eax
0018C981  752f                             jne 0x18018c9b2
0018C983  4869cd90040000                   imul rcx, rbp, 0x490
0018C98A  0fbf84311c070000                 movsx eax, word ptr [rcx + rsi + 0x71c]
0018C992  4239842ec80a0000                 cmp dword ptr [rsi + r13 + 0xac8], eax
0018C99A  7516                             jne 0x18018c9b2
0018C99C  0fbf84311e070000                 movsx eax, word ptr [rcx + rsi + 0x71e]
0018C9A4  4239842ecc0a0000                 cmp dword ptr [rsi + r13 + 0xacc], eax
0018C9AC  0f84f7010000                     je 0x18018cba9
0018C9B2  420fbf942eee060000               movsx edx, word ptr [rsi + r13 + 0x6ee]
0018C9BB  4c8d3dee23ed03                   lea r15, [rip + 0x3ed23ee]
0018C9C2  448b442464                       mov r8d, dword ptr [rsp + 0x64]
0018C9C7  488d0d920cf205                   lea rcx, [rip + 0x5f20c92]
0018C9CE  4869dd90040000                   imul rbx, rbp, 0x490
0018C9D5  4803de                           add rbx, rsi
0018C9D8  4863832c070000                   movsxd rax, dword ptr [rbx + 0x72c]
0018C9DF  450fbf8c47e0d80801               movsx r9d, word ptr [r15 + rax*2 + 0x108d8e0]
0018C9E8  420fbf842eb8090000               movsx eax, word ptr [rsi + r13 + 0x9b8]
0018C9F1  89442420                         mov dword ptr [rsp + 0x20], eax
0018C9F5  e8165cf5ff                       call 0x1800e2610
0018C9FA  85c0                             test eax, eax
0018C9FC  0f8401010000                     je 0x18018cb03
0018CA02  4533f6                           xor r14d, r14d
0018CA05  b865000000                       mov eax, 0x65
0018CA0A  664289842e18090000               mov word ptr [rsi + r13 + 0x918], ax
0018CA13  4689b42e08090000                 mov dword ptr [rsi + r13 + 0x908], r14d
0018CA1B  664639b42e48070000               cmp word ptr [rsi + r13 + 0x748], r14w
0018CA24  7524                             jne 0x18018ca4a
0018CA26  420fb7842e44070000               movzx eax, word ptr [rsi + r13 + 0x744]
0018CA2F  664289842e48070000               mov word ptr [rsi + r13 + 0x748], ax
0018CA38  420fb7842e46070000               movzx eax, word ptr [rsi + r13 + 0x746]
0018CA41  664289842e4a070000               mov word ptr [rsi + r13 + 0x74a], ax
0018CA4A  0fbf831e070000                   movsx eax, word ptr [rbx + 0x71e]
0018CA51  498bcf                           mov rcx, r15
0018CA54  440fbf8b1c070000                 movsx r9d, word ptr [rbx + 0x71c]
0018CA5C  460fbf842e1e070000               movsx r8d, word ptr [rsi + r13 + 0x71e]
0018CA65  420fbf942e1c070000               movsx edx, word ptr [rsi + r13 + 0x71c]
0018CA6E  89442420                         mov dword ptr [rsp + 0x20], eax
0018CA72  e869ceedff                       call 0x1800698e0
0018CA77  448b0db20af205                   mov r9d, dword ptr [rip + 0x5f20ab2]
0018CA7E  488bce                           mov rcx, rsi
0018CA81  448b05a40af205                   mov r8d, dword ptr [rip + 0x5f20aa4]
0018CA88  8b9424e8000000                   mov edx, dword ptr [rsp + 0xe8]
0018CA8F  4489742420                       mov dword ptr [rsp + 0x20], r14d
0018CA94  e8e7970000                       call 0x180196280
0018CA99  0fbf831c070000                   movsx eax, word ptr [rbx + 0x71c]
0018CAA0  b9ceffffff                       mov ecx, 0xffffffce
0018CAA5  4289842ec80a0000                 mov dword ptr [rsi + r13 + 0xac8], eax
0018CAAD  0fbf831e070000                   movsx eax, word ptr [rbx + 0x71e]
0018CAB4  4289842ecc0a0000                 mov dword ptr [rsi + r13 + 0xacc], eax
0018CABC  b8e2ffffff                       mov eax, 0xffffffe2
0018CAC1  2b442454                         sub eax, dword ptr [rsp + 0x54]
0018CAC5  83f8ce                           cmp eax, -0x32
0018CAC8  664689b42e4c070000               mov word ptr [rsi + r13 + 0x74c], r14w
0018CAD1  0f4cc8                           cmovl ecx, eax
0018CAD4  420fbf842e9e090000               movsx eax, word ptr [rsi + r13 + 0x99e]
0018CADD  6642898c2e040a0000               mov word ptr [rsi + r13 + 0xa04], cx
0018CAE6  3bc5                             cmp eax, ebp
0018CAE8  0f84bb000000                     je 0x18018cba9
0018CAEE  664289ac2e9e090000               mov word ptr [rsi + r13 + 0x99e], bp
0018CAF7  66ff830a0a0000                   inc word ptr [rbx + 0xa0a]
0018CAFE  e9a6000000                       jmp 0x18018cba9
0018CB03  b8f8ffffff                       mov eax, 0xfffffff8
0018CB08  33db                             xor ebx, ebx
0018CB0A  664289842e040a0000               mov word ptr [rsi + r13 + 0xa04], ax
0018CB13  420fbf842e48070000               movsx eax, word ptr [rsi + r13 + 0x748]
0018CB1C  6642899c2e9e090000               mov word ptr [rsi + r13 + 0x99e], bx
0018CB25  6685c0                           test ax, ax
0018CB28  0f847b000000                     je 0x18018cba9
0018CB2E  eb3b                             jmp 0x18018cb6b
0018CB30  664283bc2e040a000000             cmp word ptr [rsi + r13 + 0xa04], 0
0018CB3A  756d                             jne 0x18018cba9
0018CB3C  33db                             xor ebx, ebx
0018CB3E  664283bc2e1809000065             cmp word ptr [rsi + r13 + 0x918], 0x65
0018CB48  6642899c2e9e090000               mov word ptr [rsi + r13 + 0x99e], bx
0018CB51  7556                             jne 0x18018cba9
0018CB53  420fbf842e48070000               movsx eax, word ptr [rsi + r13 + 0x748]
0018CB5C  6685c0                           test ax, ax
0018CB5F  7448                             je 0x18018cba9
0018CB61  42389c2eaa0a0000                 cmp byte ptr [rsi + r13 + 0xaaa], bl
0018CB69  753e                             jne 0x18018cba9
0018CB6B  460fbf8c2e4a070000               movsx r9d, word ptr [rsi + r13 + 0x74a]
0018CB74  448bc0                           mov r8d, eax
0018CB77  8b9424e8000000                   mov edx, dword ptr [rsp + 0xe8]
0018CB7E  488bce                           mov rcx, rsi
0018CB81  895c2420                         mov dword ptr [rsp + 0x20], ebx
0018CB85  e8f6960000                       call 0x180196280
0018CB8A  b865000000                       mov eax, 0x65
0018CB8F  6642899c2e4c070000               mov word ptr [rsi + r13 + 0x74c], bx
0018CB98  664289842e18090000               mov word ptr [rsi + r13 + 0x918], ax
0018CBA1  42899c2e48070000                 mov dword ptr [rsi + r13 + 0x748], ebx
0018CBA9  b8007d0000                       mov eax, 0x7d00
0018CBAE  3bf8                             cmp edi, eax
0018CBB0  0f4ff8                           cmovg edi, eax
0018CBB3  8bc7                             mov eax, edi
0018CBB5  4881c498000000                   add rsp, 0x98
0018CBBC  415f                             pop r15
0018CBBE  415e                             pop r14
0018CBC0  415d                             pop r13
0018CBC2  415c                             pop r12
0018CBC4  5f                               pop rdi
0018CBC5  5e                               pop rsi
0018CBC6  5d                               pop rbp
0018CBC7  5b                               pop rbx
0018CBC8  c3                               ret 

distance refresh: RVA 0x19A1F0, full extent 79, role confidence candidate
0019A1F0  48895c2408                       mov qword ptr [rsp + 8], rbx
0019A1F5  57                               push rdi
0019A1F6  4883ec20                         sub rsp, 0x20
0019A1FA  4863c2                           movsxd rax, edx
0019A1FD  488bf9                           mov rdi, rcx
0019A200  4869d890040000                   imul rbx, rax, 0x490
0019A207  33c0                             xor eax, eax
0019A209  4803d9                           add rbx, rcx
0019A20C  668983040a0000                   mov word ptr [rbx + 0xa04], ax
0019A213  b801000000                       mov eax, 1
0019A218  668983540a0000                   mov word ptr [rbx + 0xa54], ax
0019A21F  e83c1fffff                       call 0x18018c160
0019A224  668983fe080000                   mov word ptr [rbx + 0x8fe], ax
0019A22B  8b471c                           mov eax, dword ptr [rdi + 0x1c]
0019A22E  8983900a0000                     mov dword ptr [rbx + 0xa90], eax
0019A234  488b5c2430                       mov rbx, qword ptr [rsp + 0x30]
0019A239  4883c420                         add rsp, 0x20
0019A23D  5f                               pop rdi
0019A23E  c3                               ret 

downstream target/attack checks: RVA 0x18E9A0, full extent 4078, role confidence candidate
0018E9A0  48894c2408                       mov qword ptr [rsp + 8], rcx
0018E9A5  53                               push rbx
0018E9A6  55                               push rbp
0018E9A7  56                               push rsi
0018E9A8  57                               push rdi
0018E9A9  4154                             push r12
0018E9AB  4155                             push r13
0018E9AD  4156                             push r14
0018E9AF  4157                             push r15
0018E9B1  4881ec88000000                   sub rsp, 0x88
0018E9B8  4533d2                           xor r10d, r10d
0018E9BB  4c63fa                           movsxd r15, edx
0018E9BE  4969f790040000                   imul rsi, r15, 0x490
0018E9C5  4c8bc1                           mov r8, rcx
0018E9C8  448954244c                       mov dword ptr [rsp + 0x4c], r10d
0018E9CD  41be801a0600                     mov r14d, 0x61a80
0018E9D3  44899424d8000000                 mov dword ptr [rsp + 0xd8], r10d
0018E9DB  498bd7                           mov rdx, r15
0018E9DE  c74424501e000000                 mov dword ptr [rsp + 0x50], 0x1e
0018E9E6  418bea                           mov ebp, r10d
0018E9E9  4489b424e8000000                 mov dword ptr [rsp + 0xe8], r14d
0018E9F1  0fbf840e10070000                 movsx eax, word ptr [rsi + rcx + 0x710]
0018E9F9  458bca                           mov r9d, r10d
0018E9FC  0fbf9c0e0e070000                 movsx ebx, word ptr [rsi + rcx + 0x70e]
0018EA04  458bde                           mov r11d, r14d
0018EA07  4c0fbfac0eee060000               movsx r13, word ptr [rsi + rcx + 0x6ee]
0018EA10  0fbf8c0e12070000                 movsx ecx, word ptr [rsi + rcx + 0x712]
0018EA18  89442444                         mov dword ptr [rsp + 0x44], eax
0018EA1C  420fbf840614070000               movsx eax, word ptr [rsi + r8 + 0x714]
0018EA25  03c8                             add ecx, eax
0018EA27  895c2448                         mov dword ptr [rsp + 0x48], ebx
0018EA2B  894c2458                         mov dword ptr [rsp + 0x58], ecx
0018EA2F  44899424e0000000                 mov dword ptr [rsp + 0xe0], r10d
0018EA37  448954245c                       mov dword ptr [rsp + 0x5c], r10d
0018EA3C  6646399406f8080000               cmp word ptr [rsi + r8 + 0x8f8], r10w
0018EA45  0f852d0f0000                     jne 0x18018f978
0018EA4B  4238ac065a0a0000                 cmp byte ptr [rsi + r8 + 0xa5a], bpl
0018EA53  0f851f0f0000                     jne 0x18018f978
0018EA59  460fbfa406e6060000               movsx r12d, word ptr [rsi + r8 + 0x6e6]
0018EA62  418d7a03                         lea edi, [r10 + 3]
0018EA66  8d4ffe                           lea ecx, [rdi - 2]
0018EA69  4c8d159015e7ff                   lea r10, [rip - 0x18ea70]
0018EA70  418d4424fa                       lea eax, [r12 - 6]
0018EA75  83f84d                           cmp eax, 0x4d
0018EA78  0f8795000000                     ja 0x18018eb13
0018EA7E  4898                             cdqe 
0018EA80  410fb68402c4f91800               movzx eax, byte ptr [r10 + rax + 0x18f9c4]
0018EA89  418b8c8290f91800                 mov ecx, dword ptr [r10 + rax*4 + 0x18f990]
0018EA91  4903ca                           add rcx, r10
0018EA94  ffe1                             jmp rcx
0018EA96  b801000000                       mov eax, 1
0018EA9B  c74424502d000000                 mov dword ptr [rsp + 0x50], 0x2d
0018EAA3  eb70                             jmp 0x18018eb15
0018EAA5  b801000000                       mov eax, 1
0018EAAA  c74424503c000000                 mov dword ptr [rsp + 0x50], 0x3c
0018EAB2  eb61                             jmp 0x18018eb15
0018EAB4  b826000000                       mov eax, 0x26
0018EAB9  eb5a                             jmp 0x18018eb15
0018EABB  b807000000                       mov eax, 7
0018EAC0  eb53                             jmp 0x18018eb15
0018EAC2  b821000000                       mov eax, 0x21
0018EAC7  eb4c                             jmp 0x18018eb15
0018EAC9  b822000000                       mov eax, 0x22
0018EACE  eb45                             jmp 0x18018eb15
0018EAD0  b802000000                       mov eax, 2
0018EAD5  89442440                         mov dword ptr [rsp + 0x40], eax
0018EAD9  664239ac06ba090000               cmp word ptr [rsi + r8 + 0x9ba], bp
0018EAE2  7f35                             jg 0x18018eb19
0018EAE4  664283bc06f409000016             cmp word ptr [rsi + r8 + 0x9f4], 0x16
0018EAEE  7429                             je 0x18018eb19
0018EAF0  e97a0e0000                       jmp 0x18018f96f
0018EAF5  8bc7                             mov eax, edi
0018EAF7  ebdc                             jmp 0x18018ead5
0018EAF9  b804000000                       mov eax, 4
0018EAFE  eb15                             jmp 0x18018eb15
0018EB00  b814000000                       mov eax, 0x14
0018EB05  eb0e                             jmp 0x18018eb15
0018EB07  b825000000                       mov eax, 0x25
0018EB0C  eb07                             jmp 0x18018eb15
0018EB0E  b901000000                       mov ecx, 1
0018EB13  8bc1                             mov eax, ecx
0018EB15  89442440                         mov dword ptr [rsp + 0x40], eax
0018EB19  8bc8                             mov ecx, eax
0018EB1B  48894c2460                       mov qword ptr [rsp + 0x60], rcx
0018EB20  418b848af0a82d00                 mov eax, dword ptr [r10 + rcx*4 + 0x2da8f0]
0018EB28  4c8d15d1986506                   lea r10, [rip + 0x66598d1]
0018EB2F  0fafc0                           imul eax, eax
0018EB32  89442454                         mov dword ptr [rsp + 0x54], eax
0018EB36  420fb78406f4090000               movzx eax, word ptr [rsi + r8 + 0x9f4]
0018EB3F  6683f804                         cmp ax, 4
0018EB43  0f8579030000                     jne 0x18018eec2
0018EB49  4e0fbfb406f6090000               movsx r14, word ptr [rsi + r8 + 0x9f6]
0018EB52  428b8406f8090000                 mov eax, dword ptr [rsi + r8 + 0x9f8]
0018EB5A  498bde                           mov rbx, r14
0018EB5D  4969ee90040000                   imul rbp, r14, 0x490
0018EB64  41398428f0060000                 cmp dword ptr [r8 + rbp + 0x6f0], eax
0018EB6C  0f8545010000                     jne 0x18018ecb7
0018EB72  6645398c28f8080000               cmp word ptr [r8 + rbp + 0x8f8], r9w
0018EB7B  0f8536010000                     jne 0x18018ecb7
0018EB81  664183bc28e406000002             cmp word ptr [r8 + rbp + 0x6e4], 2
0018EB8B  0f8526010000                     jne 0x18018ecb7
0018EB91  8bd3                             mov edx, ebx
0018EB93  498bca                           mov rcx, r10
0018EB96  e8b5b10000                       call 0x180199d50
0018EB9B  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018EBA3  4c8d1556986506                   lea r10, [rip + 0x6659856]
0018EBAA  85c0                             test eax, eax
0018EBAC  0f85f5000000                     jne 0x18018eca7
0018EBB2  664183bc2a1809000075             cmp word ptr [r10 + rbp + 0x918], 0x75
0018EBBC  0f84e5000000                     je 0x18018eca7
0018EBC2  490fbf8c28ee060000               movsx rcx, word ptr [r8 + rbp + 0x6ee]
0018EBCB  4c8d0d2e14e7ff                   lea r9, [rip - 0x18ebd2]
0018EBD2  438b84a93cdf7e03                 mov eax, dword ptr [r9 + r13*4 + 0x37edf3c]
0018EBDA  413984893cdf7e03                 cmp dword ptr [r9 + rcx*4 + 0x37edf3c], eax
0018EBE2  0f84bf000000                     je 0x18018eca7
0018EBE8  4183fc06                         cmp r12d, 6
0018EBEC  7434                             je 0x18018ec22
0018EBEE  488b8c24d0000000                 mov rcx, qword ptr [rsp + 0xd0]
0018EBF6  41b101                           mov r9b, 1
0018EBF9  448bc3                           mov r8d, ebx
0018EBFC  418bd7                           mov edx, r15d
0018EBFF  e89c7bffff                       call 0x1801867a0
0018EC04  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018EC0C  4c8d15ed976506                   lea r10, [rip + 0x66597ed]
0018EC13  85c0                             test eax, eax
0018EC15  0f858c000000                     jne 0x18018eca7
0018EC1B  4c8d0dde13e7ff                   lea r9, [rip - 0x18ec22]
0018EC22  450fbf9c2810070000               movsx r11d, word ptr [r8 + rbp + 0x710]
0018EC2B  4c8ba424d0000000                 mov r12, qword ptr [rsp + 0xd0]
0018EC33  418bc3                           mov eax, r11d
0018EC36  99                               cdq 
0018EC37  83e207                           and edx, 7
0018EC3A  410fbf9c2c0e070000               movsx ebx, word ptr [r12 + rbp + 0x70e]
0018EC43  8d0c02                           lea ecx, [rdx + rax]
0018EC46  8b442444                         mov eax, dword ptr [rsp + 0x44]
0018EC4A  99                               cdq 
0018EC4B  c1f903                           sar ecx, 3
0018EC4E  83e207                           and edx, 7
0018EC51  448d0402                         lea r8d, [rdx + rax]
0018EC55  8bc3                             mov eax, ebx
0018EC57  99                               cdq 
0018EC58  41c1f803                         sar r8d, 3
0018EC5C  442bc1                           sub r8d, ecx
0018EC5F  83e207                           and edx, 7
0018EC62  450fafc0                         imul r8d, r8d
0018EC66  8d0c02                           lea ecx, [rdx + rax]
0018EC69  8b442448                         mov eax, dword ptr [rsp + 0x48]
0018EC6D  99                               cdq 
0018EC6E  c1f903                           sar ecx, 3
0018EC71  83e207                           and edx, 7
0018EC74  03c2                             add eax, edx
0018EC76  c1f803                           sar eax, 3
0018EC79  2bc1                             sub eax, ecx
0018EC7B  0fafc0                           imul eax, eax
0018EC7E  4403c0                           add r8d, eax
0018EC81  443b442454                       cmp r8d, dword ptr [rsp + 0x54]
0018EC86  0f8e9e000000                     jle 0x18018ed2a
0018EC8C  4a0fbf8416ee060000               movsx rax, word ptr [rsi + r10 + 0x6ee]
0018EC95  4183bc81cc4b5708ff               cmp dword ptr [r9 + rax*4 + 0x8574bcc], -1
0018EC9E  0f85d40c0000                     jne 0x18018f978
0018ECA4  4d8bc4                           mov r8, r12
0018ECA7  448b8c24d8000000                 mov r9d, dword ptr [rsp + 0xd8]
0018ECAF  448b9c24e8000000                 mov r11d, dword ptr [rsp + 0xe8]
0018ECB7  420fbf840648070000               movsx eax, word ptr [rsi + r8 + 0x748]
0018ECC0  33ed                             xor ebp, ebp
0018ECC2  4289bc06f4090000                 mov dword ptr [rsi + r8 + 0x9f4], edi
0018ECCA  664289ac069c090000               mov word ptr [rsi + r8 + 0x99c], bp
0018ECD3  6685c0                           test ax, ax
0018ECD6  0f841c050000                     je 0x18018f1f8
0018ECDC  460fbf8c064a070000               movsx r9d, word ptr [rsi + r8 + 0x74a]
0018ECE5  418bd7                           mov edx, r15d
0018ECE8  488b9c24d0000000                 mov rbx, qword ptr [rsp + 0xd0]
0018ECF0  448bc0                           mov r8d, eax
0018ECF3  488bcb                           mov rcx, rbx
0018ECF6  896c2420                         mov dword ptr [rsp + 0x20], ebp
0018ECFA  e881750000                       call 0x180196280
0018ECFF  b865000000                       mov eax, 0x65
0018ED04  89ac1e48070000                   mov dword ptr [rsi + rbx + 0x748], ebp
0018ED0B  6689841e18090000                 mov word ptr [rsi + rbx + 0x918], ax
0018ED13  8d4501                           lea eax, [rbp + 1]
0018ED16  6689ac1e4c070000                 mov word ptr [rsi + rbx + 0x74c], bp
0018ED1E  89ac1e000a0000                   mov dword ptr [rsi + rbx + 0xa00], ebp
0018ED25  e9500c0000                       jmp 0x18018f97a
0018ED2A  410fbf8c2c14070000               movsx ecx, word ptr [r12 + rbp + 0x714]
0018ED33  450fbf942c12070000               movsx r10d, word ptr [r12 + rbp + 0x712]
0018ED3C  83c11a                           add ecx, 0x1a
0018ED3F  448b4c2450                       mov r9d, dword ptr [rsp + 0x50]
0018ED44  4403d1                           add r10d, ecx
0018ED47  44034c2458                       add r9d, dword ptr [rsp + 0x58]
0018ED4C  488d0d2db9de02                   lea rcx, [rip + 0x2deb92d]
0018ED53  448b442444                       mov r8d, dword ptr [rsp + 0x44]
0018ED58  8b542448                         mov edx, dword ptr [rsp + 0x48]
0018ED5C  4489542430                       mov dword ptr [rsp + 0x30], r10d
0018ED61  44895c2428                       mov dword ptr [rsp + 0x28], r11d
0018ED66  895c2420                         mov dword ptr [rsp + 0x20], ebx
0018ED6A  e88119f1ff                       call 0x1800a06f0
0018ED6F  8bd0                             mov edx, eax
0018ED71  85c0                             test eax, eax
0018ED73  0f8eeb000000                     jle 0x18018ee64
0018ED79  420fb78426e6060000               movzx eax, word ptr [rsi + r12 + 0x6e6]
0018ED82  6683f829                         cmp ax, 0x29
0018ED86  0f84d8000000                     je 0x18018ee64
0018ED8C  6683f83d                         cmp ax, 0x3d
0018ED90  0f84ce000000                     je 0x18018ee64
0018ED96  664689b4269c090000               mov word ptr [rsi + r12 + 0x99c], r14w
0018ED9F  418b8c2cf0060000                 mov ecx, dword ptr [r12 + rbp + 0x6f0]
0018EDA7  42898c26f8060000                 mov dword ptr [rsi + r12 + 0x6f8], ecx
0018EDAF  6642899426b2090000               mov word ptr [rsi + r12 + 0x9b2], dx
0018EDB8  410fb7842c0e070000               movzx eax, word ptr [r12 + rbp + 0x70e]
0018EDC1  664289842616070000               mov word ptr [rsi + r12 + 0x716], ax
0018EDCA  410fb7842c10070000               movzx eax, word ptr [r12 + rbp + 0x710]
0018EDD3  664289842618070000               mov word ptr [rsi + r12 + 0x718], ax
0018EDDC  410fb7842c12070000               movzx eax, word ptr [r12 + rbp + 0x712]
0018EDE5  66428984261a070000               mov word ptr [rsi + r12 + 0x71a], ax
0018EDEE  83fa64                           cmp edx, 0x64
0018EDF1  7f51                             jg 0x18018ee44
0018EDF3  410fbf842c12070000               movsx eax, word ptr [r12 + rbp + 0x712]
0018EDFC  410fbf942c14070000               movsx edx, word ptr [r12 + rbp + 0x714]
0018EE05  460fbf842612070000               movsx r8d, word ptr [rsi + r12 + 0x712]
0018EE0E  03d0                             add edx, eax
0018EE10  420fbf842614070000               movsx eax, word ptr [rsi + r12 + 0x714]
0018EE19  4403c0                           add r8d, eax
0018EE1C  8d4246                           lea eax, [rdx + 0x46]
0018EE1F  443bc0                           cmp r8d, eax
0018EE22  7e07                             jle 0x18018ee2b
0018EE24  b801000000                       mov eax, 1
0018EE29  eb1b                             jmp 0x18018ee46
0018EE2B  8d42ba                           lea eax, [rdx - 0x46]
0018EE2E  443bc0                           cmp r8d, eax
0018EE31  7d11                             jge 0x18018ee44
0018EE33  41b8ffffffff                     mov r8d, 0xffffffff
0018EE39  6646898426e2090000               mov word ptr [rsi + r12 + 0x9e2], r8w
0018EE42  eb0b                             jmp 0x18018ee4f
0018EE44  33c0                             xor eax, eax
0018EE46  6642898426e2090000               mov word ptr [rsi + r12 + 0x9e2], ax
0018EE4F  be01000000                       mov esi, 1
0018EE54  664189b42cf2090000               mov word ptr [r12 + rbp + 0x9f2], si
0018EE5D  8bc6                             mov eax, esi
0018EE5F  e9160b0000                       jmp 0x18018f97a
0018EE64  664283bc26e606000006             cmp word ptr [rsi + r12 + 0x6e6], 6
0018EE6E  0f84040b0000                     je 0x18018f978
0018EE74  b8feffffff                       mov eax, 0xfffffffe
0018EE79  66428984269c090000               mov word ptr [rsi + r12 + 0x99c], ax
0018EE82  410fb7842c0e070000               movzx eax, word ptr [r12 + rbp + 0x70e]
0018EE8B  664289842616070000               mov word ptr [rsi + r12 + 0x716], ax
0018EE94  410fb7842c10070000               movzx eax, word ptr [r12 + rbp + 0x710]
0018EE9D  664289842618070000               mov word ptr [rsi + r12 + 0x718], ax
0018EEA6  410fb7842c12070000               movzx eax, word ptr [r12 + rbp + 0x712]
0018EEAF  66428984261a070000               mov word ptr [rsi + r12 + 0x71a], ax
0018EEB8  b801000000                       mov eax, 1
0018EEBD  e9b80a0000                       jmp 0x18018f97a
0018EEC2  6683f822                         cmp ax, 0x22
0018EEC6  0f85dd000000                     jne 0x18018efa9
0018EECC  4a0fbf84068e090000               movsx rax, word ptr [rsi + r8 + 0x98e]
0018EED5  4c8d352411e7ff                   lea r14, [rip - 0x18eedc]
0018EEDC  488d0c80                         lea rcx, [rax + rax*4]
0018EEE0  428b8406f8090000                 mov eax, dword ptr [rsi + r8 + 0x9f8]
0018EEE8  488d1c8d00000000                 lea rbx, [rcx*4]
0018EEF0  42398433f87b0906                 cmp dword ptr [rbx + r14 + 0x6097bf8], eax
0018EEF8  0f85710a0000                     jne 0x18018f96f
0018EEFE  488d0dfb946506                   lea rcx, [rip + 0x66594fb]
0018EF05  440fbf8c0e12070000               movsx r9d, word ptr [rsi + rcx + 0x712]
0018EF0E  0fbf840e14070000                 movsx eax, word ptr [rsi + rcx + 0x714]
0018EF16  440fbf840e1e070000               movsx r8d, word ptr [rsi + rcx + 0x71e]
0018EF1F  4403c8                           add r9d, eax
0018EF22  0fbf940e1c070000                 movsx edx, word ptr [rsi + rcx + 0x71c]
0018EF2A  488d0d4fb7de02                   lea rcx, [rip + 0x2deb74f]
0018EF31  e8da13f1ff                       call 0x1800a0310
0018EF36  85c0                             test eax, eax
0018EF38  0f84290a0000                     je 0x18018f967
0018EF3E  420fb78433fe7b0906               movzx eax, word ptr [rbx + r14 + 0x6097bfe]
0018EF47  41b8ffffffff                     mov r8d, 0xffffffff
0018EF4D  488b8c24d0000000                 mov rcx, qword ptr [rsp + 0xd0]
0018EF55  66c1e003                         shl ax, 3
0018EF59  6683c004                         add ax, 4
0018EF5D  6689840e16070000                 mov word ptr [rsi + rcx + 0x716], ax
0018EF65  420fb78433007c0906               movzx eax, word ptr [rbx + r14 + 0x6097c00]
0018EF6E  66c1e003                         shl ax, 3
0018EF72  6683c004                         add ax, 4
0018EF76  6689840e18070000                 mov word ptr [rsi + rcx + 0x718], ax
0018EF7E  4a638433f47b0906                 movsxd rax, dword ptr [rbx + r14 + 0x6097bf4]
0018EF86  420fb6843050d3dd04               movzx eax, byte ptr [rax + r14 + 0x4ddd350]
0018EF8F  6689840e1a070000                 mov word ptr [rsi + rcx + 0x71a], ax
0018EF97  418d4002                         lea eax, [r8 + 2]
0018EF9B  664489840e9c090000               mov word ptr [rsi + rcx + 0x99c], r8w
0018EFA4  e9d1090000                       jmp 0x18018f97a
0018EFA9  6683f809                         cmp ax, 9
0018EFAD  0f859d000000                     jne 0x18018f050
0018EFB3  4a0fbf84068e090000               movsx rax, word ptr [rsi + r8 + 0x98e]
0018EFBC  488d153d10e7ff                   lea rdx, [rip - 0x18efc3]
0018EFC3  4869c82c030000                   imul rcx, rax, 0x32c
0018EFCA  428b8406f8090000                 mov eax, dword ptr [rsi + r8 + 0x9f8]
0018EFD2  398411e4cc4c06                   cmp dword ptr [rcx + rdx + 0x64ccce4], eax
0018EFD9  0f8590090000                     jne 0x18018f96f
0018EFDF  0fb78411facc4c06                 movzx eax, word ptr [rcx + rdx + 0x64cccfa]
0018EFE7  4c8b9c24d0000000                 mov r11, qword ptr [rsp + 0xd0]
0018EFEF  6603c0                           add ax, ax
0018EFF2  6603841104cd4c06                 add ax, word ptr [rcx + rdx + 0x64ccd04]
0018EFFA  66c1e002                         shl ax, 2
0018EFFE  664289840616070000               mov word ptr [rsi + r8 + 0x716], ax
0018F007  0fb78411fccc4c06                 movzx eax, word ptr [rcx + rdx + 0x64cccfc]
0018F00F  6603c0                           add ax, ax
0018F012  6603841104cd4c06                 add ax, word ptr [rcx + rdx + 0x64ccd04]
0018F01A  66c1e002                         shl ax, 2
0018F01E  664289840618070000               mov word ptr [rsi + r8 + 0x718], ax
0018F027  0fb78411f8cc4c06                 movzx eax, word ptr [rcx + rdx + 0x64cccf8]
0018F02F  66428984061a070000               mov word ptr [rsi + r8 + 0x71a], ax
0018F038  41b8ffffffff                     mov r8d, 0xffffffff
0018F03E  664689841e9c090000               mov word ptr [rsi + r11 + 0x99c], r8w
0018F047  418d4002                         lea eax, [r8 + 2]
0018F04B  e92a090000                       jmp 0x18018f97a
0018F050  6683f817                         cmp ax, 0x17
0018F054  0f85d0000000                     jne 0x18018f12a
0018F05A  4e0fbf8406420a0000               movsx r8, word ptr [rsi + r8 + 0xa42]
0018F063  4c8d1d960fe7ff                   lea r11, [rip - 0x18f06a]
0018F06A  4c8b9424d0000000                 mov r10, qword ptr [rsp + 0xd0]
0018F072  498bc8                           mov rcx, r8
0018F075  4b8d0440                         lea rax, [r8 + r8*2]
0018F079  4a0fbf9416400a0000               movsx rdx, word ptr [rsi + r10 + 0xa40]
0018F082  448bca                           mov r9d, edx
0018F085  45038c832cff0204                 add r9d, dword ptr [r11 + rax*4 + 0x402ff2c]
0018F08D  b81f030000                       mov eax, 0x31f
0018F092  663bd0                           cmp dx, ax
0018F095  0f87cc080000                     ja 0x18018f967
0018F09B  66443bc0                         cmp r8w, ax
0018F09F  0f87c2080000                     ja 0x18018f967
0018F0A5  4869c920030000                   imul rcx, rcx, 0x320
0018F0AC  4803ca                           add rcx, rdx
0018F0AF  4238ac19a41ea103                 cmp byte ptr [rcx + r11 + 0x3a11ea4], bpl
0018F0B7  0f84aa080000                     je 0x18018f967
0018F0BD  4963c1                           movsxd rax, r9d
0018F0C0  41f78483b0718f0400010000         test dword ptr [r11 + rax*4 + 0x48f71b0], 0x100
0018F0CC  0f8495080000                     je 0x18018f967
0018F0D2  6641c1e003                       shl r8w, 3
0018F0D7  66c1e203                         shl dx, 3
0018F0DB  664289941616070000               mov word ptr [rsi + r10 + 0x716], dx
0018F0E4  664689841618070000               mov word ptr [rsi + r10 + 0x718], r8w
0018F0ED  41b8ffffffff                     mov r8d, 0xffffffff
0018F0F3  420fb68c1870b8e204               movzx ecx, byte ptr [rax + r11 + 0x4e2b870]
0018F0FC  420fb6841850d3dd04               movzx eax, byte ptr [rax + r11 + 0x4ddd350]
0018F105  2bc1                             sub eax, ecx
0018F107  66468984169c090000               mov word ptr [rsi + r10 + 0x99c], r8w
0018F110  99                               cdq 
0018F111  2bc2                             sub eax, edx
0018F113  d1f8                             sar eax, 1
0018F115  6603c1                           add ax, cx
0018F118  66428984161a070000               mov word ptr [rsi + r10 + 0x71a], ax
0018F121  418d4002                         lea eax, [r8 + 2]
0018F125  e950080000                       jmp 0x18018f97a
0018F12A  6683f805                         cmp ax, 5
0018F12E  740a                             je 0x18018f13a
0018F130  6683f816                         cmp ax, 0x16
0018F134  0f85cc000000                     jne 0x18018f206
0018F13A  488bac24d0000000                 mov rbp, qword ptr [rsp + 0xd0]
0018F142  b81f030000                       mov eax, 0x31f
0018F147  41b8ffffffff                     mov r8d, 0xffffffff
0018F14D  4c0fbf942e400a0000               movsx r10, word ptr [rsi + rbp + 0xa40]
0018F156  4c0fbf8c2e420a0000               movsx r9, word ptr [rsi + rbp + 0xa42]
0018F15F  4d8bda                           mov r11, r10
0018F162  664489842ee2090000               mov word ptr [rsi + rbp + 0x9e2], r8w
0018F16B  443bd0                           cmp r10d, eax
0018F16E  0f87f3070000                     ja 0x18018f967
0018F174  66443bc8                         cmp r9w, ax
0018F178  0f87e9070000                     ja 0x18018f967
0018F17E  4969c920030000                   imul rcx, r9, 0x320
0018F185  4c8d35740ee7ff                   lea r14, [rip - 0x18f18c]
0018F18C  498bd9                           mov rbx, r9
0018F18F  4903ca                           add rcx, r10
0018F192  4280bc31a41ea10300               cmp byte ptr [rcx + r14 + 0x3a11ea4], 0
0018F19B  0f84c6070000                     je 0x18018f967
0018F1A1  4869d290040000                   imul rdx, rdx, 0x490
0018F1A8  6641c1e203                       shl r10w, 3
0018F1AD  488d045b                         lea rax, [rbx + rbx*2]
0018F1B1  4803d5                           add rdx, rbp
0018F1B4  6641c1e103                       shl r9w, 3
0018F1B9  664489829c090000                 mov word ptr [rdx + 0x99c], r8w
0018F1C1  6644899216070000                 mov word ptr [rdx + 0x716], r10w
0018F1C9  6644898a18070000                 mov word ptr [rdx + 0x718], r9w
0018F1D1  418b84862cff0204                 mov eax, dword ptr [r14 + rax*4 + 0x402ff2c]
0018F1D9  4103c3                           add eax, r11d
0018F1DC  4863c8                           movsxd rcx, eax
0018F1DF  420fb6843150d3dd04               movzx eax, byte ptr [rcx + r14 + 0x4ddd350]
0018F1E8  6689821a070000                   mov word ptr [rdx + 0x71a], ax
0018F1EF  418d4002                         lea eax, [r8 + 2]
0018F1F3  e982070000                       jmp 0x18018f97a
0018F1F8  8b6c244c                         mov ebp, dword ptr [rsp + 0x4c]
0018F1FC  41be801a0600                     mov r14d, 0x61a80
0018F202  8b5c2448                         mov ebx, dword ptr [rsp + 0x48]
0018F206  8b442440                         mov eax, dword ptr [rsp + 0x40]
0018F20A  83c0fe                           add eax, -2
0018F20D  83f801                           cmp eax, 1
0018F210  0f8662070000                     jbe 0x18018f978
0018F216  488d05ebe67503                   lea rax, [rip + 0x375e6eb]
0018F21D  498bcd                           mov rcx, r13
0018F220  4d69e5409c0000                   imul r12, r13, 0x9c40
0018F227  488d15d20de7ff                   lea rdx, [rip - 0x18f22e]
0018F22E  48894c2468                       mov qword ptr [rsp + 0x68], rcx
0018F233  4c03e0                           add r12, rax
0018F236  33c0                             xor eax, eax
0018F238  8944244c                         mov dword ptr [rsp + 0x4c], eax
0018F23C  4969c53c580000                   imul rax, r13, 0x583c
0018F243  488d8058ce7903                   lea rax, [rax + 0x379ce58]
0018F24A  4803c2                           add rax, rdx
0018F24D  4889442470                       mov qword ptr [rsp + 0x70], rax
0018F252  833800                           cmp dword ptr [rax], 0
0018F255  0f8e1d070000                     jle 0x18018f978
0018F25B  448b7c244c                       mov r15d, dword ptr [rsp + 0x4c]
0018F260  488d0581277303                   lea rax, [rip + 0x3732781]
0018F267  4c69e9204e0000                   imul r13, rcx, 0x4e20
0018F26E  4c03e8                           add r13, rax
0018F271  0f1f4000                         nop dword ptr [rax]
0018F275  6666660f1f840000000000           nop word ptr [rax + rax]
0018F280  490fbf4500                       movsx rax, word ptr [r13]
0018F285  4869f890040000                   imul rdi, rax, 0x490
0018F28C  8944244c                         mov dword ptr [rsp + 0x4c], eax
0018F290  664283bc07f808000000             cmp word ptr [rdi + r8 + 0x8f8], 0
0018F29A  0f850c050000                     jne 0x18018f7ac
0018F2A0  418b0424                         mov eax, dword ptr [r12]
0018F2A4  42398407f0060000                 cmp dword ptr [rdi + r8 + 0x6f0], eax
0018F2AC  0f85fa040000                     jne 0x18018f7ac
0018F2B2  460fbf8c07e6060000               movsx r9d, word ptr [rdi + r8 + 0x6e6]
0018F2BB  418d41d4                         lea eax, [r9 - 0x2c]
0018F2BF  83f82b                           cmp eax, 0x2b
0018F2C2  0f872e010000                     ja 0x18018f3f6
0018F2C8  4898                             cdqe 
0018F2CA  0fb6840230fa1800                 movzx eax, byte ptr [rdx + rax + 0x18fa30]
0018F2D2  8b8c8214fa1800                   mov ecx, dword ptr [rdx + rax*4 + 0x18fa14]
0018F2D9  4803ca                           add rcx, rdx
0018F2DC  ffe1                             jmp rcx
0018F2DE  664283bc06e606000006             cmp word ptr [rsi + r8 + 0x6e6], 6
0018F2E8  0f8408010000                     je 0x18018f3f6
0018F2EE  e9b1040000                       jmp 0x18018f7a4
0018F2F3  664283bc06e606000006             cmp word ptr [rsi + r8 + 0x6e6], 6
0018F2FD  4a0fbf840630090000               movsx rax, word ptr [rsi + r8 + 0x930]
0018F306  0f8498040000                     je 0x18018f7a4
0018F30C  6685c0                           test ax, ax
0018F30F  0f8e8f040000                     jle 0x18018f7a4
0018F315  4869c888060000                   imul rcx, rax, 0x688
0018F31C  6683bc11ae6ccc0700               cmp word ptr [rcx + rdx + 0x7cc6cae], 0
0018F325  0f8479040000                     je 0x18018f7a4
0018F32B  e9c6000000                       jmp 0x18018f3f6
0018F330  4a0fbf840730090000               movsx rax, word ptr [rdi + r8 + 0x930]
0018F339  4869c888060000                   imul rcx, rax, 0x688
0018F340  6683bc11266dcc0700               cmp word ptr [rcx + rdx + 0x7cc6d26], 0
0018F349  0f8555040000                     jne 0x18018f7a4
0018F34F  6683bc11286dcc0700               cmp word ptr [rcx + rdx + 0x7cc6d28], 0
0018F358  0f8598000000                     jne 0x18018f3f6
0018F35E  b8cf000000                       mov eax, 0xcf
0018F363  664239840718090000               cmp word ptr [rdi + r8 + 0x918], ax
0018F36C  0f8532040000                     jne 0x18018f7a4
0018F372  4a0fbf8407f6090000               movsx rax, word ptr [rdi + r8 + 0x9f6]
0018F37B  4869c890040000                   imul rcx, rax, 0x490
0018F382  4280bc018409000000               cmp byte ptr [rcx + r8 + 0x984], 0
0018F38B  0f8513040000                     jne 0x18018f7a4
0018F391  eb63                             jmp 0x18018f3f6
0018F393  4a0fbf8406ee060000               movsx rax, word ptr [rsi + r8 + 0x6ee]
0018F39C  4a0fbf8c07d8060000               movsx rcx, word ptr [rdi + r8 + 0x6d8]
0018F3A5  8b84823cdf7e03                   mov eax, dword ptr [rdx + rax*4 + 0x37edf3c]
0018F3AC  39848a3cdf7e03                   cmp dword ptr [rdx + rcx*4 + 0x37edf3c], eax
0018F3B3  7541                             jne 0x18018f3f6
0018F3B5  e9ea030000                       jmp 0x18018f7a4
0018F3BA  4281bc17900a0000a0000000         cmp dword ptr [rdi + r10 + 0xa90], 0xa0
0018F3C6  eb28                             jmp 0x18018f3f0
0018F3C8  4283bc17900a000050               cmp dword ptr [rdi + r10 + 0xa90], 0x50
0018F3D1  7e23                             jle 0x18018f3f6
0018F3D3  420fb7841718090000               movzx eax, word ptr [rdi + r10 + 0x918]
0018F3DC  6683e86a                         sub ax, 0x6a
0018F3E0  6683f801                         cmp ax, 1
0018F3E4  7610                             jbe 0x18018f3f6
0018F3E6  664283bc179c0a000078             cmp word ptr [rdi + r10 + 0xa9c], 0x78
0018F3F0  0f8fae030000                     jg 0x18018f7a4
0018F3F6  460fbf940710070000               movsx r10d, word ptr [rdi + r8 + 0x710]
0018F3FF  4c8b9c24d0000000                 mov r11, qword ptr [rsp + 0xd0]
0018F407  418bc2                           mov eax, r10d
0018F40A  99                               cdq 
0018F40B  83e207                           and edx, 7
0018F40E  460fbf9c1f0e070000               movsx r11d, word ptr [rdi + r11 + 0x70e]
0018F417  8d0c02                           lea ecx, [rdx + rax]
0018F41A  8b442444                         mov eax, dword ptr [rsp + 0x44]
0018F41E  99                               cdq 
0018F41F  c1f903                           sar ecx, 3
0018F422  83e207                           and edx, 7
0018F425  448d0402                         lea r8d, [rdx + rax]
0018F429  418bc3                           mov eax, r11d
0018F42C  99                               cdq 
0018F42D  41c1f803                         sar r8d, 3
0018F431  442bc1                           sub r8d, ecx
0018F434  83e207                           and edx, 7
0018F437  450fafc0                         imul r8d, r8d
0018F43B  8d0c02                           lea ecx, [rdx + rax]
0018F43E  8bc3                             mov eax, ebx
0018F440  99                               cdq 
0018F441  c1f903                           sar ecx, 3
0018F444  83e207                           and edx, 7
0018F447  03c2                             add eax, edx
0018F449  c1f803                           sar eax, 3
0018F44C  2bc1                             sub eax, ecx
0018F44E  0fafc0                           imul eax, eax
0018F451  4103c0                           add eax, r8d
0018F454  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018F45C  3b442454                         cmp eax, dword ptr [rsp + 0x54]
0018F460  0f8f2f030000                     jg 0x18018f795
0018F466  8b542444                         mov edx, dword ptr [rsp + 0x44]
0018F46A  8bc3                             mov eax, ebx
0018F46C  412bc3                           sub eax, r11d
0018F46F  418bcb                           mov ecx, r11d
0018F472  2bcb                             sub ecx, ebx
0018F474  413bdb                           cmp ebx, r11d
0018F477  418bda                           mov ebx, r10d
0018F47A  0f4fc8                           cmovg ecx, eax
0018F47D  2bda                             sub ebx, edx
0018F47F  8bc2                             mov eax, edx
0018F481  412bc2                           sub eax, r10d
0018F484  413bd2                           cmp edx, r10d
0018F487  0f4fd8                           cmovg ebx, eax
0018F48A  420fbf8407340a0000               movsx eax, word ptr [rdi + r8 + 0xa34]
0018F493  3bcb                             cmp ecx, ebx
0018F495  0f4dd9                           cmovge ebx, ecx
0018F498  6bc832                           imul ecx, eax, 0x32
0018F49B  03d9                             add ebx, ecx
0018F49D  488b4c2460                       mov rcx, qword ptr [rsp + 0x60]
0018F4A2  4883f904                         cmp rcx, 4
0018F4A6  7570                             jne 0x18018f518
0018F4A8  4183c1e2                         add r9d, -0x1e
0018F4AC  4183f92f                         cmp r9d, 0x2f
0018F4B0  771e                             ja 0x18018f4d0
0018F4B2  488d15470be7ff                   lea rdx, [rip - 0x18f4b9]
0018F4B9  4963c1                           movsxd rax, r9d
0018F4BC  0fb6840264fa1800                 movzx eax, byte ptr [rdx + rax + 0x18fa64]
0018F4C4  8b8c825cfa1800                   mov ecx, dword ptr [rdx + rax*4 + 0x18fa5c]
0018F4CB  4803ca                           add rcx, rdx
0018F4CE  ffe1                             jmp rcx
0018F4D0  488d15290be7ff                   lea rdx, [rip - 0x18f4d7]
0018F4D7  4a0fbf8406ee060000               movsx rax, word ptr [rsi + r8 + 0x6ee]
0018F4E0  83bc82cc4b5708ff                 cmp dword ptr [rdx + rax*4 + 0x8574bcc], -1
0018F4E8  0f85ae020000                     jne 0x18018f79c
0018F4EE  664283bc07fc08000000             cmp word ptr [rdi + r8 + 0x8fc], 0
0018F4F8  7408                             je 0x18018f502
0018F4FA  8d1c5b                           lea ebx, [rbx + rbx*2]
0018F4FD  83c364                           add ebx, 0x64
0018F500  eb08                             jmp 0x18018f50a
0018F502  83c30a                           add ebx, 0xa
0018F505  8d1c9b                           lea ebx, [rbx + rbx*4]
0018F508  03db                             add ebx, ebx
0018F50A  83fbff                           cmp ebx, -1
0018F50D  0f8489020000                     je 0x18018f79c
0018F513  e97b010000                       jmp 0x18018f693
0018F518  4883f914                         cmp rcx, 0x14
0018F51C  7538                             jne 0x18018f556
0018F51E  4183c1ea                         add r9d, -0x16
0018F522  488d15d70ae7ff                   lea rdx, [rip - 0x18f529]
0018F529  4183f93f                         cmp r9d, 0x3f
0018F52D  0f8758010000                     ja 0x18018f68b
0018F533  4963c1                           movsxd rax, r9d
0018F536  0fb68402a4fa1800                 movzx eax, byte ptr [rdx + rax + 0x18faa4]
0018F53E  8b8c8294fa1800                   mov ecx, dword ptr [rdx + rax*4 + 0x18fa94]
0018F545  4803ca                           add rcx, rdx
0018F548  ffe1                             jmp rcx
0018F54A  8d1c5d64000000                   lea ebx, [rbx*2 + 0x64]
0018F551  e93d010000                       jmp 0x18018f693
0018F556  4883f925                         cmp rcx, 0x25
0018F55A  755c                             jne 0x18018f5b8
0018F55C  4183c1fb                         add r9d, -5
0018F560  488d15990ae7ff                   lea rdx, [rip - 0x18f567]
0018F567  4183f950                         cmp r9d, 0x50
0018F56B  0f872b020000                     ja 0x18018f79c
0018F571  4963c1                           movsxd rax, r9d
0018F574  0fb68402f8fa1800                 movzx eax, byte ptr [rdx + rax + 0x18faf8]
0018F57C  8b8c82e4fa1800                   mov ecx, dword ptr [rdx + rax*4 + 0x18fae4]
0018F583  4803ca                           add rcx, rdx
0018F586  ffe1                             jmp rcx
0018F588  8d1c5d64000000                   lea ebx, [rbx*2 + 0x64]
0018F58F  eb19                             jmp 0x18018f5aa
0018F591  488b442468                       mov rax, qword ptr [rsp + 0x68]
0018F596  83bc82cc4b5708ff                 cmp dword ptr [rdx + rax*4 + 0x8574bcc], -1
0018F59E  0f84f8010000                     je 0x18018f79c
0018F5A4  83c328                           add ebx, 0x28
0018F5A7  8d1c9b                           lea ebx, [rbx + rbx*4]
0018F5AA  83fbff                           cmp ebx, -1
0018F5AD  0f84e9010000                     je 0x18018f79c
0018F5B3  e9db000000                       jmp 0x18018f693
0018F5B8  420fb68407570a0000               movzx eax, byte ptr [rdi + r8 + 0xa57]
0018F5C1  4883f926                         cmp rcx, 0x26
0018F5C5  7558                             jne 0x18018f61f
0018F5C7  488d15320ae7ff                   lea rdx, [rip - 0x18f5ce]
0018F5CE  84c0                             test al, al
0018F5D0  7428                             je 0x18018f5fa
0018F5D2  4a0fbf8407ee060000               movsx rax, word ptr [rdi + r8 + 0x6ee]
0018F5DB  83bc82cc4b5708ff                 cmp dword ptr [rdx + rax*4 + 0x8574bcc], -1
0018F5E3  7515                             jne 0x18018f5fa
0018F5E5  b856555555                       mov eax, 0x55555556
0018F5EA  f7eb                             imul ebx
0018F5EC  8bda                             mov ebx, edx
0018F5EE  c1eb1f                           shr ebx, 0x1f
0018F5F1  03da                             add ebx, edx
0018F5F3  488d15060ae7ff                   lea rdx, [rip - 0x18f5fa]
0018F5FA  4183c1ea                         add r9d, -0x16
0018F5FE  4183f93f                         cmp r9d, 0x3f
0018F602  0f8783000000                     ja 0x18018f68b
0018F608  4963c1                           movsxd rax, r9d
0018F60B  0fb6840264fb1800                 movzx eax, byte ptr [rdx + rax + 0x18fb64]
0018F613  8b8c824cfb1800                   mov ecx, dword ptr [rdx + rax*4 + 0x18fb4c]
0018F61A  4803ca                           add rcx, rdx
0018F61D  ffe1                             jmp rcx
0018F61F  488d15da09e7ff                   lea rdx, [rip - 0x18f626]
0018F626  84c0                             test al, al
0018F628  7428                             je 0x18018f652
0018F62A  4a0fbf8407ee060000               movsx rax, word ptr [rdi + r8 + 0x6ee]
0018F633  83bc82cc4b5708ff                 cmp dword ptr [rdx + rax*4 + 0x8574bcc], -1
0018F63B  7515                             jne 0x18018f652
0018F63D  b856555555                       mov eax, 0x55555556
0018F642  f7eb                             imul ebx
0018F644  8bda                             mov ebx, edx
0018F646  c1eb1f                           shr ebx, 0x1f
0018F649  03da                             add ebx, edx
0018F64B  488d15ae09e7ff                   lea rdx, [rip - 0x18f652]
0018F652  4183c1ea                         add r9d, -0x16
0018F656  4183f93f                         cmp r9d, 0x3f
0018F65A  772f                             ja 0x18018f68b
0018F65C  4963c1                           movsxd rax, r9d
0018F65F  0fb68402bcfb1800                 movzx eax, byte ptr [rdx + rax + 0x18fbbc]
0018F667  8b8c82a4fb1800                   mov ecx, dword ptr [rdx + rax*4 + 0x18fba4]
0018F66E  4803ca                           add rcx, rdx
0018F671  ffe1                             jmp rcx
0018F673  83c34b                           add ebx, 0x4b
0018F676  eb1b                             jmp 0x18018f693
0018F678  83c328                           add ebx, 0x28
0018F67B  8d1c9b                           lea ebx, [rbx + rbx*4]
0018F67E  eb13                             jmp 0x18018f693
0018F680  6bdb07                           imul ebx, ebx, 7
0018F683  81c32c010000                     add ebx, 0x12c
0018F689  eb08                             jmp 0x18018f693
0018F68B  83c328                           add ebx, 0x28
0018F68E  8d1c9b                           lea ebx, [rbx + rbx*4]
0018F691  03db                             add ebx, ebx
0018F693  413bde                           cmp ebx, r14d
0018F696  7c0d                             jl 0x18018f6a5
0018F698  3b9c24e8000000                   cmp ebx, dword ptr [rsp + 0xe8]
0018F69F  0f8df7000000                     jge 0x18018f79c
0018F6A5  420fbf8c0714070000               movsx ecx, word ptr [rdi + r8 + 0x714]
0018F6AE  420fbf940712070000               movsx edx, word ptr [rdi + r8 + 0x712]
0018F6B7  83c11a                           add ecx, 0x1a
0018F6BA  448b4c2450                       mov r9d, dword ptr [rsp + 0x50]
0018F6BF  03d1                             add edx, ecx
0018F6C1  44034c2458                       add r9d, dword ptr [rsp + 0x58]
0018F6C6  488d0db3afde02                   lea rcx, [rip + 0x2deafb3]
0018F6CD  448b442444                       mov r8d, dword ptr [rsp + 0x44]
0018F6D2  89542430                         mov dword ptr [rsp + 0x30], edx
0018F6D6  8b542448                         mov edx, dword ptr [rsp + 0x48]
0018F6DA  4489542428                       mov dword ptr [rsp + 0x28], r10d
0018F6DF  44895c2420                       mov dword ptr [rsp + 0x20], r11d
0018F6E4  e80710f1ff                       call 0x1800a06f0
0018F6E9  448b8c24d8000000                 mov r9d, dword ptr [rsp + 0xd8]
0018F6F1  8bd0                             mov edx, eax
0018F6F3  85c0                             test eax, eax
0018F6F5  0f8e81000000                     jle 0x18018f77c
0018F6FB  4c8b9c24d0000000                 mov r11, qword ptr [rsp + 0xd0]
0018F703  413bde                           cmp ebx, r14d
0018F706  448b44244c                       mov r8d, dword ptr [rsp + 0x4c]
0018F70B  8bcb                             mov ecx, ebx
0018F70D  440f4cc8                         cmovl r9d, eax
0018F711  410f4dce                         cmovge ecx, r14d
0018F715  418bc0                           mov eax, r8d
0018F718  44898c24d8000000                 mov dword ptr [rsp + 0xd8], r9d
0018F720  0f4dc5                           cmovge eax, ebp
0018F723  448bf1                           mov r14d, ecx
0018F726  664283bc1ff209000000             cmp word ptr [rdi + r11 + 0x9f2], 0
0018F730  8be8                             mov ebp, eax
0018F732  448b9c24e8000000                 mov r11d, dword ptr [rsp + 0xe8]
0018F73A  752f                             jne 0x18018f76b
0018F73C  413bdb                           cmp ebx, r11d
0018F73F  7d2a                             jge 0x18018f76b
0018F741  8954245c                         mov dword ptr [rsp + 0x5c], edx
0018F745  418bf8                           mov edi, r8d
0018F748  44898424e0000000                 mov dword ptr [rsp + 0xe0], r8d
0018F750  488d15a908e7ff                   lea rdx, [rip - 0x18f757]
0018F757  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018F75F  448bdb                           mov r11d, ebx
0018F762  899c24e8000000                   mov dword ptr [rsp + 0xe8], ebx
0018F769  eb48                             jmp 0x18018f7b3
0018F76B  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018F773  488d158608e7ff                   lea rdx, [rip - 0x18f77a]
0018F77A  eb30                             jmp 0x18018f7ac
0018F77C  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018F784  488d157508e7ff                   lea rdx, [rip - 0x18f78b]
0018F78B  448b9c24e8000000                 mov r11d, dword ptr [rsp + 0xe8]
0018F793  eb17                             jmp 0x18018f7ac
0018F795  488d156408e7ff                   lea rdx, [rip - 0x18f79c]
0018F79C  448b9c24e8000000                 mov r11d, dword ptr [rsp + 0xe8]
0018F7A4  448b8c24d8000000                 mov r9d, dword ptr [rsp + 0xd8]
0018F7AC  8bbc24e0000000                   mov edi, dword ptr [rsp + 0xe0]
0018F7B3  488b442470                       mov rax, qword ptr [rsp + 0x70]
0018F7B8  4c8d15418c6506                   lea r10, [rip + 0x6658c41]
0018F7BF  8b5c2448                         mov ebx, dword ptr [rsp + 0x48]
0018F7C3  41ffc7                           inc r15d
0018F7C6  4983c502                         add r13, 2
0018F7CA  4983c404                         add r12, 4
0018F7CE  443b38                           cmp r15d, dword ptr [rax]
0018F7D1  0f8ca9faffff                     jl 0x18018f280
0018F7D7  448b7c2440                       mov r15d, dword ptr [rsp + 0x40]
0018F7DC  85ed                             test ebp, ebp
0018F7DE  0f8494010000                     je 0x18018f978
0018F7E4  85ff                             test edi, edi
0018F7E6  7415                             je 0x18018f7fd
0018F7E8  438d0476                         lea eax, [r14 + r14*2]
0018F7EC  99                               cdq 
0018F7ED  2bc2                             sub eax, edx
0018F7EF  d1f8                             sar eax, 1
0018F7F1  443bd8                           cmp r11d, eax
0018F7F4  7d07                             jge 0x18018f7fd
0018F7F6  448b4c245c                       mov r9d, dword ptr [rsp + 0x5c]
0018F7FB  8bef                             mov ebp, edi
0018F7FD  4183ff04                         cmp r15d, 4
0018F801  7558                             jne 0x18018f85b
0018F803  b8feffffff                       mov eax, 0xfffffffe
0018F808  66428984069c090000               mov word ptr [rsi + r8 + 0x99c], ax
0018F811  4863c5                           movsxd rax, ebp
0018F814  4869c890040000                   imul rcx, rax, 0x490
0018F81B  420fb784010e070000               movzx eax, word ptr [rcx + r8 + 0x70e]
0018F824  664289840616070000               mov word ptr [rsi + r8 + 0x716], ax
0018F82D  420fb7840110070000               movzx eax, word ptr [rcx + r8 + 0x710]
0018F836  664289840618070000               mov word ptr [rsi + r8 + 0x718], ax
0018F83F  420fb7840112070000               movzx eax, word ptr [rcx + r8 + 0x712]
0018F848  66428984061a070000               mov word ptr [rsi + r8 + 0x71a], ax
0018F851  b801000000                       mov eax, 1
0018F856  e91f010000                       jmp 0x18018f97a
0018F85B  4183ff25                         cmp r15d, 0x25
0018F85F  0f84d1000000                     je 0x18018f936
0018F865  4183ff14                         cmp r15d, 0x14
0018F869  0f84c7000000                     je 0x18018f936
0018F86F  4863c5                           movsxd rax, ebp
0018F872  4869d090040000                   imul rdx, rax, 0x490
0018F879  4903d0                           add rdx, r8
0018F87C  66ff82340a0000                   inc word ptr [rdx + 0xa34]
0018F883  664289ac069c090000               mov word ptr [rsi + r8 + 0x99c], bp
0018F88C  664289ac06360a0000               mov word ptr [rsi + r8 + 0xa36], bp
0018F895  8b82f0060000                     mov eax, dword ptr [rdx + 0x6f0]
0018F89B  42898406f8060000                 mov dword ptr [rsi + r8 + 0x6f8], eax
0018F8A3  6646898c06b2090000               mov word ptr [rsi + r8 + 0x9b2], r9w
0018F8AC  4183f964                         cmp r9d, 0x64
0018F8B0  7e0d                             jle 0x18018f8bf
0018F8B2  33c0                             xor eax, eax
0018F8B4  6642898406e2090000               mov word ptr [rsi + r8 + 0x9e2], ax
0018F8BD  eb64                             jmp 0x18018f923
0018F8BF  4c8b9424d0000000                 mov r10, qword ptr [rsp + 0xd0]
0018F8C7  0fbf8212070000                   movsx eax, word ptr [rdx + 0x712]
0018F8CE  440fbf8214070000                 movsx r8d, word ptr [rdx + 0x714]
0018F8D6  4403c0                           add r8d, eax
0018F8D9  420fbf841614070000               movsx eax, word ptr [rsi + r10 + 0x714]
0018F8E2  460fbf8c1612070000               movsx r9d, word ptr [rsi + r10 + 0x712]
0018F8EB  4403c8                           add r9d, eax
0018F8EE  418d4046                         lea eax, [r8 + 0x46]
0018F8F2  443bc8                           cmp r9d, eax
0018F8F5  7e07                             jle 0x18018f8fe
0018F8F7  b801000000                       mov eax, 1
0018F8FC  eb1c                             jmp 0x18018f91a
0018F8FE  418d40ba                         lea eax, [r8 - 0x46]
0018F902  443bc8                           cmp r9d, eax
0018F905  7d11                             jge 0x18018f918
0018F907  41b8ffffffff                     mov r8d, 0xffffffff
0018F90D  6646898416e2090000               mov word ptr [rsi + r10 + 0x9e2], r8w
0018F916  eb0b                             jmp 0x18018f923
0018F918  33c0                             xor eax, eax
0018F91A  6642898416e2090000               mov word ptr [rsi + r10 + 0x9e2], ax
0018F923  41ba01000000                     mov r10d, 1
0018F929  66448992f2090000                 mov word ptr [rdx + 0x9f2], r10w
0018F931  418bc2                           mov eax, r10d
0018F934  eb44                             jmp 0x18018f97a
0018F936  4863c5                           movsxd rax, ebp
0018F939  4869c890040000                   imul rcx, rax, 0x490
0018F940  664289ac069c090000               mov word ptr [rsi + r8 + 0x99c], bp
0018F949  428b8401f0060000                 mov eax, dword ptr [rcx + r8 + 0x6f0]
0018F951  42898406f8060000                 mov dword ptr [rsi + r8 + 0x6f8], eax
0018F959  6646898c06b2090000               mov word ptr [rsi + r8 + 0x9b2], r9w
0018F962  e9b4feffff                       jmp 0x18018f81b
0018F967  4c8b8424d0000000                 mov r8, qword ptr [rsp + 0xd0]
0018F96F  664289bc06f4090000               mov word ptr [rsi + r8 + 0x9f4], di
0018F978  33c0                             xor eax, eax
0018F97A  4881c488000000                   add rsp, 0x88
0018F981  415f                             pop r15
0018F983  415e                             pop r14
0018F985  415d                             pop r13
0018F987  415c                             pop r12
0018F989  5f                               pop rdi
0018F98A  5e                               pop rsi
0018F98B  5d                               pop rbp
0018F98C  5b                               pop rbx
0018F98D  c3                               ret 
