#region License
/* 
 * Copyright (C) 1999-2016 John K�ll�n.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.CLanguage;
using Reko.Core.Configuration;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Rtl;
using Reko.Core.Serialization;
using Reko.Core.Services;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reko.Environments.Windows
{
	public class Win32Platform : Platform
	{
        private Dictionary<int, SystemService> services;

        //$TODO: http://www.delorie.com/djgpp/doc/rbinter/ix/29.html int 29 for console apps!
        //$TODO: http://msdn.microsoft.com/en-us/data/dn774154(v=vs.99).aspx

        //$TODO: we need a Win32Base platform, possibly with a Windows base platform, and make this
        // x86-specific.
        public Win32Platform(IServiceProvider services, IProcessorArchitecture arch) : base(services, arch, "win32")
        {
            var frame = arch.CreateFrame();
            this.services = new Dictionary<int, SystemService>
            {
                {
                    3,
                    new SystemService
                    {
                        SyscallInfo = new SyscallInfo
                        {
                            Vector = 3,
                            RegisterValues = new RegValue[0],
                        },
                        Name = "int3",
                        Signature = new ProcedureSignature(null, new Identifier[0]),
                        Characteristics = new ProcedureCharacteristics(),
                    }
                },
                {
                    0x29,
                    new SystemService
                    {
                        SyscallInfo = new SyscallInfo
                        {
                            Vector = 0x29,
                            RegisterValues = new RegValue[0]
                        },
                        Name = "__fastfail",
                        Signature = new ProcedureSignature(
                            null,
                            frame.EnsureRegister(Registers.ecx)), //$bug what about win64?
                        Characteristics = new ProcedureCharacteristics
                        {
                            Terminates = true
                        }
                    }
                }
            };
        }

        public override HashSet<RegisterStorage> CreateImplicitArgumentRegisters()
        {
            var bitset = new HashSet<RegisterStorage>()
            {
                 Registers.cs,
                 Registers.ss,
                 Registers.sp,
                 Registers.esp,
                 Registers.fs,
                 Registers.gs,
            };
            return bitset;
        }

        public override ProcedureSerializer CreateProcedureSerializer(ISerializedTypeVisitor<DataType> typeLoader, string defaultConvention)
        {
            return new X86ProcedureSerializer((IntelArchitecture) Architecture, typeLoader, defaultConvention);
        }

        public override ImageSymbol FindMainProcedure(Program program, Address addrStart)
        {
            var sf = new X86StartFinder(program, addrStart);
            return sf.FindMainProcedure();
        }

        //$REFACTOR: should be loaded from config file.
        public override int GetByteSizeFromCBasicType(CBasicType cb)
        {
            switch (cb)
            {
            case CBasicType.Char: return 1;
            case CBasicType.Short: return 2;
            case CBasicType.Int: return 4;
            case CBasicType.Long: return 4;
            case CBasicType.LongLong: return 8;
            case CBasicType.Float: return 4;
            case CBasicType.Double: return 8;
            case CBasicType.LongDouble: return 8;
            case CBasicType.Int64: return 8;
            default: throw new NotImplementedException(string.Format("C basic type {0} not supported.", cb));
            }
        }

        //$REFACTOR: should fetch this from config file?
        public override string GetPrimitiveTypeName(PrimitiveType pt, string language)
        {
            if (language != "C")
                return null;
            switch (pt.Domain)
            {
            case Domain.Character:
                switch (pt.Size)
                {
                case 1: return "char";
                case 2: return "wchar_t";
                }
                break;
            case Domain.SignedInt:
                switch (pt.Size)
                {
                case 1: return "signed char";
                case 2: return "short";
                case 4: return "int";
                case 8: return "__int64";
                }
                break;
            case Domain.UnsignedInt:
                switch (pt.Size)
                {
                case 1: return "unsigned char";
                case 2: return "unsigned short";
                case 4: return "unsigned int";
                case 8: return "unsigned __int64";
                }
                break;
            case Domain.Real:
                switch (pt.Size)
                {
                case 4: return "float";
                case 8: return "double";
                }
                break;
            }
            return null;
        }

        public override ProcedureBase GetTrampolineDestination(ImageReader rdr, IRewriterHost host)
        {
            var rw = Architecture.CreateRewriter(
                rdr,
                Architecture.CreateProcessorState(),
                Architecture.CreateFrame(), host);
            var rtlc = rw.FirstOrDefault();
            if (rtlc == null || rtlc.Instructions.Count == 0)
                return null;
            var jump = rtlc.Instructions[0] as RtlGoto;
            if (jump == null)
                return null;
            var pc = jump.Target as ProcedureConstant;
            if (pc != null)
                return pc.Procedure;
            var access = jump.Target as MemoryAccess;
            if (access == null)
                return null;
            var addrTarget = access.EffectiveAddress as Address;
            if (addrTarget == null)
            {
                var wAddr = access.EffectiveAddress as Constant;
                if (wAddr == null)
                {
                    return null;
                }
                addrTarget = MakeAddressFromConstant(wAddr);
            }
            ProcedureBase proc = host.GetImportedProcedure(addrTarget, rtlc.Address);
            if (proc != null)
                return proc;
            return host.GetInterceptedCall(addrTarget);
        }

        public override ExternalProcedure LookupProcedureByName(string moduleName, string procName)
        {
            EnsureTypeLibraries(PlatformIdentifier);
            ModuleDescriptor mod;
            if (Metadata.Modules.TryGetValue(moduleName.ToUpper(), out mod))
            {
                SystemService svc;
                if (mod.ServicesByName.TryGetValue(procName, out svc))
                {
                    return new ExternalProcedure(svc.Name, svc.Signature);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                ProcedureSignature sig;
                if (Metadata.Signatures.TryGetValue(procName, out sig))
                    return new ExternalProcedure(procName, sig);
                else
                    return null;
            }
        }

        public override ExternalProcedure LookupProcedureByOrdinal(string moduleName, int ordinal)
        {
            EnsureTypeLibraries(PlatformIdentifier);
            ModuleDescriptor mod;
            if (!Metadata.Modules.TryGetValue(moduleName.ToUpper(), out mod))
                return null;
            SystemService svc;
            if (mod.ServicesByVector.TryGetValue(ordinal, out svc))
            {
                return new ExternalProcedure(svc.Name, svc.Signature);
            }
            else
                return null;
        }

		public override SystemService FindService(int vector, ProcessorState state)
		{
            SystemService svc;
            if (!services.TryGetValue(vector, out svc))
                throw new NotImplementedException("INT service {0} is not supported by Windows.");
            return svc;
		}

        public override string DefaultCallingConvention
        {
            get { return "__cdecl"; }
        }

        public override ExternalProcedure SignatureFromName(string fnName)
        {
            EnsureTypeLibraries(PlatformIdentifier); 
            return SignatureGuesser.SignatureFromName(
                fnName, 
                new TypeLibraryDeserializer(this, true, Metadata),
                this);
        }
	}
}
