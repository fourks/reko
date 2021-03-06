﻿#region License
/* 
 * Copyright (C) 1999-2016 John Källén.
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

using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Operators;
using Reko.Core.Rtl;
using Reko.Core.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Arch.Z80
{
    public class Z80Rewriter : IEnumerable<RtlInstructionCluster>
    {
        private Z80ProcessorArchitecture arch;
        private ProcessorState state;
        private Frame frame;
        private IRewriterHost host;
        private IEnumerator<Z80Instruction> dasm;
        private RtlInstructionCluster rtlc;
        private RtlEmitter emitter;

        public Z80Rewriter(Z80ProcessorArchitecture arch, ImageReader rdr, ProcessorState state, Frame frame, IRewriterHost host)
        {
            this.arch = arch;
            this.state = state;
            this.frame = frame;
            this.host = host;
            this.dasm = new Z80Disassembler(rdr).GetEnumerator();
        }

        public IEnumerator<RtlInstructionCluster> GetEnumerator()
        {
            while (dasm.MoveNext())
            {
                rtlc = new RtlInstructionCluster(dasm.Current.Address, dasm.Current.Length);
                rtlc.Class = RtlClass.Linear;
                emitter = new RtlEmitter(rtlc.Instructions);
                switch (dasm.Current.Code)
                {
                default: throw new AddressCorrelatedException(
                    dasm.Current.Address,
                    "Rewriting of Z80 instruction {0} not implemented yet.",
                    dasm.Current.Code);
                case Opcode.adc: RewriteAdc(); break;
                case Opcode.add: RewriteAdd(); break;
                case Opcode.and: RewriteAnd(); break;
                case Opcode.call: RewriteCall(dasm.Current); break;
                case Opcode.cp: RewriteCp(); break;
                case Opcode.cpl: RewriteCpl(); break;
                case Opcode.daa: RewriteDaa(); break;
                case Opcode.dec: RewriteDec(); break;
                case Opcode.djnz: RewriteDjnz(dasm.Current.Op1); break;
                case Opcode.ex: RewriteEx(); break;
                case Opcode.hlt: emitter.SideEffect(PseudoProc("__hlt", VoidType.Instance)); break;
                case Opcode.im:
                    emitter.SideEffect(PseudoProc("__im", VoidType.Instance, RewriteOp(dasm.Current.Op1)));
                    break;
                case Opcode.inc: RewriteInc(); break;
                case Opcode.jp: RewriteJp(dasm.Current); break;
                case Opcode.jr: RewriteJr(); break;
                case Opcode.ld: emitter.Assign(
                    RewriteOp(dasm.Current.Op1),
                    RewriteOp(dasm.Current.Op2));
                    break;
                case Opcode.rl: RewriteRotation(PseudoProcedure.RolC, true); break;
                case Opcode.rla: RewriteRotation(PseudoProcedure.Rol, false); break;
                case Opcode.rlc: RewriteRotation(PseudoProcedure.RolC, false); break;
                case Opcode.rlca: RewriteRotation(PseudoProcedure.RolC, false); break;
                case Opcode.rra: RewriteRotation(PseudoProcedure.Ror, true); break;
                case Opcode.rrc: RewriteRotation(PseudoProcedure.RorC, true); break;
                case Opcode.ldir: RewriteBlockInstruction(); break;
                case Opcode.neg: RewriteNeg(); break;
                case Opcode.nop: emitter.Nop(); break;
                case Opcode.or: RewriteOr(); break;
                case Opcode.pop: RewritePop(); break;
                case Opcode.push: RewritePush(dasm.Current); break;
                case Opcode.ret: RewriteRet(); break;
                case Opcode.sbc: RewriteSbc(); break;
                case Opcode.sla: RewriteShift(dasm.Current, emitter.Shl); break;
                case Opcode.sra: RewriteShift(dasm.Current, emitter.Sar); break;
                case Opcode.srl: RewriteShift(dasm.Current, emitter.Shr); break;
                case Opcode.sub: RewriteSub(); break;
                case Opcode.xor: RewriteXor(); break;

                //$TODO: Not implemented yet; feel free to implement these!
        case Opcode.di: goto default;
        case Opcode.ei: goto default;
        case Opcode.bit: goto default;
        case Opcode.ccf: goto default;
        case Opcode.cpd: goto default;
        case Opcode.cpdr: goto default;
        case Opcode.cpi: goto default;
        case Opcode.cpir: goto default;
        case Opcode.ex_af: goto default;
        case Opcode.exx: goto default;
        case Opcode.ldd: goto default;
        case Opcode.lddr: goto default;
        case Opcode.ldi: goto default;
        case Opcode.@in: goto default;
        case Opcode.ind: goto default;
        case Opcode.indr: goto default;
        case Opcode.ini: goto default;
        case Opcode.inir: goto default;
        case Opcode.otdr: goto default;
        case Opcode.otir: goto default;
        case Opcode.@out: goto default;
        case Opcode.outd: goto default;
        case Opcode.outi: goto default;
        case Opcode.outr: goto default;
        case Opcode.res: goto default;
        case Opcode.reti: goto default;
        case Opcode.retn: goto default;
        case Opcode.rld: goto default;
        case Opcode.rr: goto default;
        case Opcode.rrd: goto default;
        case Opcode.rrca: goto default;
        case Opcode.rst: goto default;
        case Opcode.scf: goto default;
        case Opcode.set: goto default;
        case Opcode.swap: goto default;
                }
                yield return rtlc;
            }
        }

        //$REVIEW: push PseudoProc into the RewriterHost interface"
        public Expression PseudoProc(string name, DataType retType, params Expression[] args)
        {
            var ppp = host.EnsurePseudoProcedure(name, retType, args.Length);
            return PseudoProc(ppp, retType, args);
        }

        public Expression PseudoProc(PseudoProcedure ppp, DataType retType, params Expression[] args)
        {
            if (args.Length != ppp.Arity)
                throw new ArgumentOutOfRangeException(
                    string.Format("Pseudoprocedure {0} expected {1} arguments, but was passed {2}.",
                    ppp.Name,
                    ppp.Arity,
                    args.Length));

            return emitter.Fn(new ProcedureConstant(arch.PointerType, ppp), retType, args);
        }

        private void RewriteAdc()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.IAdd(emitter.IAdd(dst, src), FlagGroup(FlagM.CF)));
            AssignCond(FlagM.CF | FlagM.ZF | FlagM.SF | FlagM.PF, dst);
        }

        private void RewriteAdd()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.IAdd(dst, src));
            AssignCond(FlagM.CF | FlagM.ZF | FlagM.SF | FlagM.PF, dst);
        }

        private void RewriteAnd()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.And(dst, src));
            AssignCond(FlagM.ZF | FlagM.SF | FlagM.CF, dst);
            emitter.Assign(FlagGroup(FlagM.CF), Constant.False());
        }

        private void RewriteBlockInstruction()
        {
            var bc = frame.EnsureRegister(Registers.bc);
            var de = frame.EnsureRegister(Registers.de);
            var hl = frame.EnsureRegister(Registers.hl);
            var V =  FlagGroup(FlagM.PF);
            emitter.Assign(emitter.LoadB(de), emitter.LoadB(hl));
            emitter.Assign(hl, emitter.IAdd(hl, 1));
            emitter.Assign(de, emitter.IAdd(de, 1));
            emitter.Assign(bc, emitter.ISub(bc, 1));
            emitter.BranchInMiddleOfInstruction(emitter.Ne0(bc), dasm.Current.Address, RtlClass.Transfer);
            emitter.Assign(V, emitter.Const(PrimitiveType.Bool, 0));
        }

        private void RewriteNeg()
        {
            var a = frame.EnsureRegister(Registers.a);
            emitter.Assign(a, emitter.Neg(a));
            AssignCond(FlagM.SF | FlagM.ZF | FlagM.PF | FlagM.CF, a);
        }

        private void RewriteOr()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.ISub(dst, src));
            AssignCond(FlagM.ZF | FlagM.SF | FlagM.CF, dst);
            emitter.Assign(FlagGroup(FlagM.CF), Constant.False());
        }

        private void RewriteSbc()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.ISub(emitter.ISub(dst, src), FlagGroup(FlagM.CF)));
            AssignCond(FlagM.CF | FlagM.ZF | FlagM.SF | FlagM.PF, dst);
        }

        private void RewriteRotation(string pseudoOp, bool useCarry)
        {
            Expression reg;
            if (dasm.Current.Op1 != null)
            {
                reg = RewriteOp(dasm.Current.Op1);
            }
            else
            {
                reg = frame.EnsureRegister(Registers.a);
            }
            var C = FlagGroup(FlagM.CF);
            Expression src;
            if (useCarry)
            {
                src = emitter.Fn(
                    new PseudoProcedure(pseudoOp, reg.DataType, 2),
                    reg, C);
            }
            else 
            {
                src = emitter.Fn(
                    new PseudoProcedure(pseudoOp, reg.DataType, 1),
                    reg);
            }
            emitter.Assign(reg, src);
            emitter.Assign(C, emitter.Cond(reg));
        }

        private void RewriteSub()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.ISub(dst, src));
            AssignCond(FlagM.CF | FlagM.ZF | FlagM.SF | FlagM.CF, dst);
        }

        private void RewriteXor()
        {
            var dst = RewriteOp(dasm.Current.Op1);
            var src = RewriteOp(dasm.Current.Op2);
            emitter.Assign(dst, emitter.Xor(dst, src));
            AssignCond(FlagM.ZF | FlagM.SF | FlagM.CF, dst);
            emitter.Assign(FlagGroup(FlagM.CF), Constant.False());
        }

        private void AssignCond(FlagM flags, Expression dst)
        {
            emitter.Assign(FlagGroup(flags), emitter.Cond(dst));
        }

        public Identifier FlagGroup(FlagM flags)
        {
            return frame.EnsureFlagGroup(Registers.f,(uint) flags, arch.GrfToString((uint) flags), PrimitiveType.Byte);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void EmitBranch(ConditionOperand cOp, Address dst)
        {
            rtlc.Class = RtlClass.ConditionalTransfer;
            emitter.Branch(
                GenerateTestExpression(cOp, false),
                dst,
                RtlClass.ConditionalTransfer);
        }

        private TestCondition GenerateTestExpression(ConditionOperand cOp, bool invert)
        {
            ConditionCode cc = ConditionCode.ALWAYS;
            FlagM flags = 0;
            switch (cOp.Code)
            {
            case CondCode.nz:  cc = invert ? ConditionCode.EQ : ConditionCode.NE; flags = FlagM.ZF;  break;
            case CondCode.z: cc = invert ? ConditionCode.NE : ConditionCode.EQ; flags = FlagM.ZF;    break;
            case CondCode.nc: cc = invert ? ConditionCode.ULT : ConditionCode.UGE; flags = FlagM.CF; break;
            case CondCode.c: cc = invert ? ConditionCode.UGE : ConditionCode.ULT; flags = FlagM.CF;  break;
            case CondCode.po: cc = invert ? ConditionCode.PE : ConditionCode.PO; flags = FlagM.PF;  break;
            case CondCode.pe: cc = invert ? ConditionCode.PO : ConditionCode.PE; flags = FlagM.PF;    break;
            case CondCode.p: cc = invert ? ConditionCode.SG : ConditionCode.NS; flags = FlagM.PF;    break;
            case CondCode.m: cc = invert ? ConditionCode.NS : ConditionCode.SG; flags = FlagM.PF;    break;
            }
            return emitter.Test(
                cc,
                FlagGroup(flags));
        }

        private void RewriteCall(Z80Instruction instr)
        {
            rtlc.Class = RtlClass.Transfer;
            var cOp = instr.Op1 as ConditionOperand;
            if (cOp != null)
            {
                emitter.BranchInMiddleOfInstruction(
                    GenerateTestExpression(cOp, true),
                    instr.Address + instr.Length,
                    RtlClass.ConditionalTransfer);
                emitter.Call(((AddressOperand) instr.Op2).Address, 2);
            }
            else
            {
                emitter.Call(((AddressOperand) instr.Op1).Address, 2);
            }
        }

        private void RewriteCp()
        {
            var a = this.RewriteOp(dasm.Current.Op1);
            var b = this.RewriteOp(dasm.Current.Op2);
            emitter.Assign(
                FlagGroup(FlagM.SF | FlagM.ZF | FlagM.CF | FlagM.PF),
                emitter.ISub(a, b));
        }

        private void RewriteCpl()
        {
            var a = frame.EnsureRegister(Registers.a);
            emitter.Assign(a, emitter.Comp(a));
        }

        private void RewriteDaa()
        {
            var a = frame.EnsureRegister(Registers.a);
            emitter.Assign(
                a,
                PseudoProc("__daa", PrimitiveType.Byte, a));
            AssignCond(FlagM.CF | FlagM.ZF | FlagM.SF | FlagM.PF, a);
        }

        private void RewriteDec()
        {
            var src = RewriteOp(dasm.Current.Op1);
            var dst = RewriteOp(dasm.Current.Op1);
            emitter.Assign(dst, emitter.ISub(src, 1));
            AssignCond(FlagM.ZF | FlagM.SF | FlagM.PF, dst);
        }

        private void RewriteDjnz(MachineOperand dst)
        {
            rtlc.Class = RtlClass.Linear;
            var b = frame.EnsureRegister(Registers.b);
            emitter.Assign(b, emitter.ISub(b, 1));
            emitter.Branch(emitter.Ne0(b), ((AddressOperand)dst).Address, RtlClass.Transfer);
        }

        private void RewriteEx()
        {
            var t = frame.CreateTemporary(dasm.Current.Op1.Width);
            emitter.Assign(t, RewriteOp(dasm.Current.Op1));
            emitter.Assign(RewriteOp(dasm.Current.Op1), RewriteOp(dasm.Current.Op2));
            emitter.Assign(RewriteOp(dasm.Current.Op2), t);
        }
    
        private void RewriteInc()
        {
            var src = RewriteOp( dasm.Current.Op1);
            var dst = RewriteOp( dasm.Current.Op1);
            emitter.Assign(dst, emitter.IAdd(src, 1));
            AssignCond(FlagM.ZF | FlagM.SF | FlagM.PF, dst);
        }

 
        private void RewriteJp(Z80Instruction instr)
        {
            rtlc.Class = RtlClass.Transfer;
            var cOp = instr.Op1 as ConditionOperand;
            if (cOp != null)
            {
                EmitBranch(cOp, ((AddressOperand) instr.Op2).Address);
            }
            else
            {
                var target = instr.Op1 as AddressOperand;
                if (target != null)
                {
                    rtlc.Class = RtlClass.Transfer;
                    emitter.Goto(target.Address);
                }
                var mTarget = instr.Op1 as MemoryOperand;
                if(mTarget != null)
                {
                    emitter.Goto(frame.EnsureRegister(mTarget.Base));
                }
            }
        }

        private void RewriteJr()
        {
            rtlc.Class = RtlClass.Transfer;
            var target = (AddressOperand) dasm.Current.Op1;
            emitter.Goto(target.Address);
        }

        private Expression RewriteOp(MachineOperand op)
        {
            var rOp = op as RegisterOperand;
            if (rOp != null)
                return frame.EnsureRegister(rOp.Register);
            var immOp = op as ImmediateOperand;
            if (immOp != null)
                return immOp.Value;
            var memOp = op as MemoryOperand;
            if (memOp != null)
            {
                Identifier bReg = null;
                if (memOp.Base != null)
                    bReg = frame.EnsureRegister(memOp.Base);
                if (memOp.Offset == null)
                {
                    return emitter.Load(memOp.Width, bReg);
                }
                else if (bReg == null)
                {
                    return emitter.Load(memOp.Width, memOp.Offset);
                }
                else
                {
                    int s = memOp.Offset.ToInt32();
                    if (s > 0)
                    {
                        return emitter.Load(memOp.Width, emitter.IAdd(bReg, s));
                    }
                    else if (s < 0)
                    {
                        return emitter.Load(memOp.Width, emitter.ISub(bReg, -s));
                    }
                    else
                    {
                        return emitter.Load(memOp.Width, bReg);
                    }
                }
            }
            throw new NotImplementedException(string.Format("Rewriting of Z80 operand type {0} is not implemented yet.", op.GetType().FullName));
        }

        private void RewritePop()
        {
            var sp = frame.EnsureRegister(Registers.sp);
            var op = RewriteOp(dasm.Current.Op1);
            emitter.Assign(op, emitter.Load(PrimitiveType.Word16, sp));
            emitter.Assign(sp, emitter.IAdd(sp, op.DataType.Size));
        }

        private void RewritePush(Z80Instruction instr)
        {
            var sp = frame.EnsureRegister(Registers.sp);
            var op = RewriteOp(instr.Op1);
            emitter.Assign(sp, emitter.ISub(sp, op.DataType.Size));
            emitter.Assign(emitter.Load(PrimitiveType.Word16, sp), op);
        }

        private void RewriteRet()
        {
            rtlc.Class = RtlClass.Transfer;
            emitter.Return(2, 0);
        }

        private void RewriteShift(Z80Instruction instr, Func<Expression, Expression, Expression> op)
        {
            var reg = RewriteOp(instr.Op1);
            var sh = emitter.Byte(1);
            emitter.Assign(reg, op(reg, sh));
            AssignCond(FlagM.CF | FlagM.ZF | FlagM.SF | FlagM.PF, reg);
        }
    }
}
