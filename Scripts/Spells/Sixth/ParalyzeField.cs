using System;
using Server.Targeting;
using Server.Items;
using Server.Network;
using Server.Misc;
using Server.Mobiles;

namespace Server.Spells.Sixth
{
	public class ParalyzeFieldSpell : MagerySpell
	{
		private static SpellInfo m_Info = new SpellInfo(
				"Paralyze Field", "In Ex Grav",
				230,
				9012,
				false,
				Reagent.BlackPearl,
				Reagent.Ginseng,
				Reagent.SpidersSilk
			);

        // EFFECT=
        public override double CastDelayFastScalar { get { return 0; } }
        public override TimeSpan CastDelayBase { get { return TimeSpan.FromSeconds(1.7); } }
        public override bool ClearHandsOnCast { get { return false; } }
        public override int ScaleMana(int mana) { return 20; }

		public override SpellCircle Circle { get { return SpellCircle.Sixth; } }
        public override void SelectTarget()
        {
            Caster.Target = new InternalSphereTarget(this);
        }

        public override void OnSphereCast()
        {
            if (SpellTarget != null)
            {
                if (SpellTarget is IPoint3D)
                {
                    Target((IPoint3D)SpellTarget);
                }
                else
                {
                    Caster.SendAsciiMessage("Alvo invalido");
                }
            }
            FinishSequence();
        }

	    public ParalyzeFieldSpell( Mobile caster, Item scroll ) : base( caster, scroll, m_Info )
		{
		}

		public override void OnCast()
		{
			Caster.Target = new InternalTarget( this );
		}

		public void Target( IPoint3D p )
		{
			if ( !Caster.CanSee( p ) )
			{
				Caster.SendLocalizedMessage( 500237 ); // Target can not be seen.
			}
            else if (!CheckLineOfSight(p))
            {
                this.DoFizzle();
                Caster.SendAsciiMessage("Target is not in line of sight");
            }
			else if ( SpellHelper.CheckTown( p, Caster ) && CheckSequence() )
			{
				SpellHelper.Turn( Caster, p );

				SpellHelper.GetSurfaceTop( ref p );

				int dx = Caster.Location.X - p.X;
				int dy = Caster.Location.Y - p.Y;
				int rx = (dx - dy) * 44;
				int ry = (dx + dy) * 44;

				bool eastToWest;

				if ( rx >= 0 && ry >= 0 )
					eastToWest = false;
				else if ( rx >= 0 )
					eastToWest = true;
				else if ( ry >= 0 )
					eastToWest = true;
				else
					eastToWest = false;

				Effects.PlaySound( p, Caster.Map, 0x20B );

				int itemID = eastToWest ? 0x3967 : 0x3979;

				TimeSpan duration = TimeSpan.FromSeconds( 3.0 + (Caster.Skills[SkillName.Magery].Value / 3.0) );

				for ( int i = -2; i <= 2; ++i )
				{
					Point3D loc = new Point3D( eastToWest ? p.X + i : p.X, eastToWest ? p.Y : p.Y + i, p.Z );
					bool canFit = SpellHelper.AdjustField( ref loc, Caster.Map, 12, false );

					if ( !canFit )
						continue;

					Item item = new InternalItem( Caster, itemID, loc, Caster.Map, duration );
					item.ProcessDelta();

					Effects.SendLocationParticles( EffectItem.Create( loc, Caster.Map, EffectItem.DefaultDuration ), 0x376A, 9, 10, 5048 );
				}
			}

			FinishSequence();
		}

        private class InternalSphereTarget : Target
        {
            private ParalyzeFieldSpell m_Owner;

            public InternalSphereTarget(ParalyzeFieldSpell owner)
                : base(Core.ML ? 10 : 12, true, TargetFlags.Harmful)
            {
                m_Owner = owner;
                m_Owner.Caster.SendAsciiMessage("Selecione o alvo...");
            }

            protected override void OnTarget(Mobile from, object o)
            {
                if (o is IPoint3D)
                {
                    m_Owner.SpellTarget = o;
                    m_Owner.CastSpell();
                }
                else
                {
                    m_Owner.Caster.SendAsciiMessage("Alvo invalido");
                }
            }

            protected override void OnTargetFinish(Mobile from)
            {
                if (m_Owner.SpellTarget == null)
                {
                    m_Owner.Caster.SendAsciiMessage("Target cancelado.");
                }
            }
        }

		[DispellableField]
		public class InternalItem : Item
		{
			private Timer m_Timer;
			private Mobile m_Caster;
			private DateTime m_End;

			public override bool BlocksFit{ get{ return true; } }

			public InternalItem( Mobile caster, int itemID, Point3D loc, Map map, TimeSpan duration ) : base( itemID )
			{
				Visible = false;
				Movable = false;
				Light = LightType.Circle300;

				MoveToWorld( loc, map );

				if ( caster.InLOS( this ) )
					Visible = true;
				else
					Delete();

				if ( Deleted )
					return;

				m_Caster = caster;

				m_Timer = new InternalTimer( this, duration );
				m_Timer.Start();

				m_End = DateTime.Now + duration;
			}

			public override void OnAfterDelete()
			{
				base.OnAfterDelete();

				if ( m_Timer != null )
					m_Timer.Stop();
			}

			public InternalItem( Serial serial ) : base( serial )
			{
			}

			public override void Serialize( GenericWriter writer )
			{
				base.Serialize( writer );

				writer.Write( (int) 0 ); // version

				writer.Write( m_Caster );
				writer.WriteDeltaTime( m_End );
			}

			public override void Deserialize( GenericReader reader )
			{
				base.Deserialize( reader );

				int version = reader.ReadInt();

				switch ( version )
				{
					case 0:
					{
						m_Caster = reader.ReadMobile();
						m_End = reader.ReadDeltaTime();

						m_Timer = new InternalTimer( this, m_End - DateTime.Now );
						m_Timer.Start();

						break;
					}
				}
			}

			public override bool OnMoveOver( Mobile m )
			{
				if ( Visible && m_Caster != null && (!Core.AOS || m != m_Caster) && SpellHelper.ValidIndirectTarget( m_Caster, m ) && m_Caster.CanBeHarmful( m, false ) )
				{
					if ( SpellHelper.CanRevealCaster( m ) )
						m_Caster.RevealingAction();

					m_Caster.DoHarmful( m );

					double duration;

					if ( Core.AOS )
					{
						duration = 2.0 + ((int)(m_Caster.Skills[SkillName.EvalInt].Value / 10) - (int)(m.Skills[SkillName.MagicResist].Value / 10));

						if ( !m.Player )
							duration *= 3.0;

						if ( duration < 0.0 )
							duration = 0.0;
					}
					else
					{
						duration = 7.0 + (m_Caster.Skills[SkillName.Magery].Value * 0.2);
					}

					m.Paralyze( TimeSpan.FromSeconds( duration ) );

					m.PlaySound( 0x204 );
					m.FixedEffect( 0x376A, 10, 16 );
				}

				return true;
			}

			private class InternalTimer : Timer
			{
				private Item m_Item;

				public InternalTimer( Item item, TimeSpan duration ) : base( duration )
				{
					Priority = TimerPriority.OneSecond;
					m_Item = item;
				}

				protected override void OnTick()
				{
					m_Item.Delete();
				}
			}
		}

		private class InternalTarget : Target
		{
			private ParalyzeFieldSpell m_Owner;

			public InternalTarget( ParalyzeFieldSpell owner ) : base( Core.ML ? 10 : 12, true, TargetFlags.None )
			{
				m_Owner = owner;
			}

			protected override void OnTarget( Mobile from, object o )
			{
				if ( o is IPoint3D )
					m_Owner.Target( (IPoint3D)o );
			}

			protected override void OnTargetFinish( Mobile from )
			{
				m_Owner.FinishSequence();
			}
		}
	}
}