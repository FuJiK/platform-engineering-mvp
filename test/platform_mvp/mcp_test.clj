(ns platform-mvp.mcp-test
  (:require [clojure.test :refer [deftest is testing]]
            [platform-mvp.mcp :as mcp]
            [platform-mvp.docker :as docker]))

(def sample-policy
  {:service "demo-web"
   :environment "local"
   :operations
   {:get-status {:roles ["operator" "sre"]}
    :get-logs {:roles ["operator" "sre"] :maxLines 200}
    :restart-service {:roles ["sre"] :approval "required"}}})

(deftest json-rpc-result-shape
  (is (= {:jsonrpc "2.0" :id 1 :result {:ok true}}
         (mcp/json-rpc-result 1 {:ok true}))))

(deftest initialize-negotiates-protocol-version
  (let [result (:result (mcp/handle-request sample-policy :operator
                                            {:id 1
                                             :method "initialize"
                                             :params {:protocolVersion "2025-11-25"}}))]
    (is (= "2025-11-25" (:protocolVersion result)))
    (is (= "platform-engineering-mvp" (get-in result [:serverInfo :name])))))

(deftest tools-list-respects-role
  (let [operator-tools (:result (mcp/handle-request sample-policy :operator
                                                    {:id 2 :method "tools/list" :params {}}))
        sre-tools (:result (mcp/handle-request sample-policy :sre
                                              {:id 3 :method "tools/list" :params {}}))]
    (is (= #{"get_status" "get_logs"}
         (set (map :name (:tools operator-tools))))
    (is (= #{"get_status" "get_logs" "restart_service"}
         (set (map :name (:tools sre-tools)))))))

(deftest tools-call-rejects-unknown-tool
  (let [response (mcp/handle-request sample-policy :operator
                                     {:id 4
                                      :method "tools/call"
                                      :params {:name "delete_everything" :arguments {}}})]
    (is (= -32602 (get-in response [:error :code])))
    (is (re-find #"Unknown tool" (get-in response [:error :message])))))

(deftest tools-call-enforces-restart-approval
  (with-redefs [docker/restart-service (fn [_] "restarted")]
    (let [denied (:result (mcp/handle-request sample-policy :sre
                                              {:id 5
                                               :method "tools/call"
                                               :params {:name "restart_service"
                                                        :arguments {:approved false}}}))
          approved (:result (mcp/handle-request sample-policy :sre
                                                {:id 6
                                                 :method "tools/call"
                                                 :params {:name "restart_service"
                                                          :arguments {:approved true}}}))]
      (is (:isError denied))
      (is (re-find #"approved=true" (get-in denied [:content 0 :text])))
      (is (not (:isError approved)))
      (is (re-find #"restarted=" (get-in approved [:content 0 :text]))))))

(deftest operator-cannot-call-restart-even-directly
  (with-redefs [docker/restart-service (fn [_] (throw (ex-info "should not run" {})))]
    (let [response (:result (mcp/handle-request sample-policy :operator
                                                {:id 7
                                                 :method "tools/call"
                                                 :params {:name "restart_service"
                                                          :arguments {:approved true}}}))]
      (is (:isError response))
      (is (re-find #"not allowed" (get-in response [:content 0 :text]))))))

(deftest get-logs-enforces-max-lines
  (with-redefs [docker/get-logs (fn [_ lines] (str "lines=" lines))]
    (let [ok (:result (mcp/handle-request sample-policy :operator
                                          {:id 8
                                           :method "tools/call"
                                           :params {:name "get_logs"
                                                    :arguments {:lines 50}}}))
          bad (:result (mcp/handle-request sample-policy :operator
                                           {:id 9
                                            :method "tools/call"
                                            :params {:name "get_logs"
                                                     :arguments {:lines 500}}}))]
      (is (re-find #"lines=50" (get-in ok [:content 0 :text])))
      (is (:isError bad))
      (is (re-find #"permitted range" (get-in bad [:content 0 :text]))))))

(deftest ping-returns-empty-result
  (is (= {:jsonrpc "2.0" :id 99 :result {}}
         (mcp/handle-request sample-policy :operator
                             {:id 99 :method "ping" :params {}})))))
