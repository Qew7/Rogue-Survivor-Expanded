#!/usr/bin/env ruby
require 'tmpdir'
require 'rbconfig'

Dir.mktmpdir('npc-flamegraph-test') do |dir|
  report = File.join(dir, 'calls.txt')
  svg = File.join(dir, 'flamegraph.svg')
  folded = File.join(dir, 'flamegraph.folded')
  File.write(report, <<~REPORT)
    Method call summary
    Total(ms) Self(ms) Calls Method name
       100       10       1 NpcTurnBenchmarks:RunTurns (Fixture)
        40       20       4 djack.RogueSurvivor.Gameplay.AI.Widget<Thing>:Do (int)
	3 calls from:
		NpcTurnBenchmarks:RunTurns (Fixture)
	1 calls from:
		Outside:Call ()
  REPORT

  script = File.join(__dir__, 'npc_flamegraph.rb')
  raise 'flamegraph generator failed' unless system(RbConfig.ruby, script, report, svg, folded)

  expected = [
    'NpcTurnBenchmarks:RunTurns 10000',
    'NpcTurnBenchmarks:RunTurns;Other / native / rounded 75000',
    'NpcTurnBenchmarks:RunTurns;Gameplay.AI.Widget<Thing>:Do 15000'
  ]
  actual = File.readlines(folded, chomp: true)
  raise "attributed stacks changed: #{actual.inspect}" unless actual == expected

  image = File.read(svg, encoding: 'UTF-8')
  raise 'SVG labels are not escaped' unless image.include?('Widget&lt;Thing&gt;:Do')
  raise 'SVG total changed' unless image.include?('Mono total: 100 ms · attributed: 100.0 ms')
end

puts 'NPC flamegraph test passed'
