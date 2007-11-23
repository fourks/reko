/* 
 * Copyright (C) 1999-2007 John K�ll�n.
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

using Decompiler.Analysis;
using Decompiler.Arch.Intel;
using Decompiler.Core;
using Decompiler.Core.Code;
using Decompiler.Core.Types;
using System;
using NUnit.Framework;

namespace Decompiler.UnitTests.Analysis
{
	[TestFixture]
	public class IsLiveHelperTests
	{
		private Frame f = new Frame(PrimitiveType.Word32);
		private IdentifierLiveness liveness;
		private RegisterLiveness.IsLiveHelper isLiveHelper;

		[SetUp]
		public void Setup()
		{
			liveness = new IdentifierLiveness(new IntelArchitecture(ProcessorMode.ProtectedFlat));
			isLiveHelper = new RegisterLiveness.IsLiveHelper();
		}

		[Test]
		public void IsRegisterLive()
		{
			liveness.BitSet = new Decompiler.Core.Lib.BitSet(64);
			liveness.BitSet[Registers.ecx.Number] = true;
			Identifier eax = f.EnsureRegister(Registers.eax);
			Identifier ecx = f.EnsureRegister(Registers.ecx);
			Assert.IsTrue(isLiveHelper.IsLive(ecx, liveness), "ECX should be live");
			Assert.IsFalse(isLiveHelper.IsLive(eax, liveness), "EAX should be dead");

		}
	}
}