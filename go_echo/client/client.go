package main

import (
	"encoding/binary"
	"flag"
	"fmt"
	"io"
	"net"
	"sync"
	"time"

	"github.com/montanaflynn/stats"
)

var (
	g_address string
	g_port    int
	g_round   int
	g_payload int
	g_clients int
	g_tricky  bool
)

func init() {
	flag.IntVar(&g_port, "p", 10008, "server port")
	flag.StringVar(&g_address, "l", "127.0.0.1", "server adress")
	flag.IntVar(&g_payload, "s", 64, "payload size")
	flag.IntVar(&g_round, "r", 2, "echo round")
	flag.IntVar(&g_clients, "c", 5000, "clients number")
	flag.BoolVar(&g_tricky, "tricky", false, "tricky")
	flag.Parse()
}

func main() {
	connectTime := make([]float64, g_clients)
	echoTime := make([]float64, g_clients)
	totalTime := make([]float64, g_clients)

	var wg sync.WaitGroup
	wg.Add(g_clients)

	payload := make([]byte, g_payload)
	server := fmt.Sprintf("%s:%d", g_address, g_port)

	begin := time.Now()
	for i := 0; i < g_clients; i++ {
		go func(idx int) {
			defer wg.Done()
			buffer := make([]byte, 4+len(payload))

			beginConn := time.Now()
			conn, err := net.Dial("tcp", server)
			if err != nil {
				panic(err)
			}
			conn.(*net.TCPConn).SetNoDelay(true)
			defer conn.Close()
			finishConn := time.Now()

			for j := 0; j < g_round; j++ {
				binary.LittleEndian.PutUint32(buffer, uint32(len(payload)))
				copy(buffer[4:], payload)
				_, err = conn.Write(buffer[:len(payload)+4])
				if err != nil {
					panic(err)
				}
				if g_tricky {
					_, err = io.ReadFull(conn, buffer)
					if err != nil {
						panic(err)
					}

				} else {

					_, err = io.ReadFull(conn, buffer[:4])
					if err != nil {
						panic(err)
					}

					size := int(binary.LittleEndian.Uint32(buffer[:4]))
					if cap(buffer) < 4+size {
						buffer = make([]byte, 4+size)
					}

					_, err = io.ReadFull(conn, buffer[4:4+size])
					if err != nil {
						panic(err)
					}
				}
			}
			echoFinish := time.Now()
			connectTime[idx] = finishConn.Sub(beginConn).Seconds() * 1000
			echoTime[idx] = echoFinish.Sub(finishConn).Seconds() * 1000
			totalTime[idx] = echoFinish.Sub(beginConn).Seconds() * 1000
		}(i)
	}
	wg.Wait()
	end := time.Now()

	percentile := func(name string, data []float64) {
		var (
			p90  float64
			p95  float64
			p99  float64
			p999 float64
			err  error
		)
		if p90, err = stats.Percentile(data, 90.0); err != nil {
			panic(err)
		}
		if p95, err = stats.Percentile(data, 95.0); err != nil {
			panic(err)
		}
		if p99, err = stats.Percentile(data, 99.0); err != nil {
			panic(err)
		}
		if p999, err = stats.Percentile(data, 99.9); err != nil {
			panic(err)
		}
		fmt.Printf("%s\tp90:%.2fms\tp95:%.2fms\tp99:%.2fms\tp99.9:%.2fms\r\n", name, p90, p95, p99, p999)
	}

	fmt.Printf("%d clients, payload %d bytes, %d rounds\r\n", g_clients, g_payload, g_round)
	fmt.Printf("Total Time Elapsed: %f Milliseconds\r\n", end.Sub(begin).Seconds()*1000)
	percentile("connect", connectTime)
	percentile("echo", echoTime)
	percentile("total", totalTime)
}
